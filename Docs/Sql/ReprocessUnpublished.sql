-- Run against the HiringPs SQL Server database after updating the app and collector.
-- Requeues all source items not shared with published/archived opportunities.
-- Draft/review/hidden opportunities in that batch lose manual override and are re-extracted.
-- Source content, revision history, original links and social publication history are retained.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'HiringPs.Pipeline',
        @LockMode = 'Exclusive',
        @LockOwner = 'Transaction',
        @LockTimeout = 0;

    IF @LockResult < 0
        THROW 51001, N'توجد جولة جمع قيد التنفيذ؛ أعد التنفيذ بعد انتهائها.', 1;

    DECLARE @PromptVersionId int;
    SELECT @PromptVersionId = COALESCE(PendingPromptVersionId, ActivePromptVersionId)
    FROM dbo.WorkflowSettings WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = 1;

    IF @PromptVersionId IS NULL
        THROW 51002, N'لم يتم العثور على نسخة البرومبت المطلوبة.', 1;

    DECLARE @Items TABLE (Id bigint PRIMARY KEY);
    INSERT INTO @Items (Id)
    SELECT item.Id
    FROM dbo.SourceItems AS item WITH (UPDLOCK, HOLDLOCK)
    WHERE NOT EXISTS (
        SELECT 1
        FROM dbo.SourceLinks AS link WITH (HOLDLOCK)
        INNER JOIN dbo.Opportunities AS opportunity WITH (UPDLOCK, HOLDLOCK)
            ON opportunity.Id = link.OpportunityId
        WHERE link.SourceItemId = item.Id
          AND opportunity.Status IN (2, 3) -- Published, Archived
    );

    UPDATE opportunity
    SET Status = 0, -- Draft: temporary until the new extraction is applied
        AdminOverride = 0
    FROM dbo.Opportunities AS opportunity
    WHERE opportunity.Status NOT IN (2, 3)
      AND EXISTS (
          SELECT 1
          FROM dbo.SourceLinks AS link
          INNER JOIN @Items AS queued ON queued.Id = link.SourceItemId
          WHERE link.OpportunityId = opportunity.Id
      );
    DECLARE @OpportunitiesCount int = @@ROWCOUNT;

    UPDATE item
    SET Status = 0, -- Discovered: fetch original source before AI
        Attempts = 0,
        RetryAt = NULL,
        LastVerifiedAt = NULL,
        Error = N'',
        ExtractionJson = N'',
        ExtractionPromptVersionId = NULL,
        RequestedPromptVersionId = @PromptVersionId
    FROM dbo.SourceItems AS item
    INNER JOIN @Items AS queued ON queued.Id = item.Id;
    DECLARE @ItemsCount int = @@ROWCOUNT;

    -- Report exceptions; never silently move a protected shared source into the queue.
    DECLARE @Skipped TABLE (Id bigint PRIMARY KEY, Title nvarchar(max), Reason nvarchar(200));
    INSERT INTO @Skipped (Id, Title, Reason)
    SELECT opportunity.Id, opportunity.Title,
        CASE WHEN NOT EXISTS (
            SELECT 1 FROM dbo.SourceLinks AS link WHERE link.OpportunityId = opportunity.Id
        ) THEN N'لا يوجد عنصر مصدر مرتبط'
          ELSE N'المصدر مشترك مع فرصة منشورة أو مؤرشفة' END
    FROM dbo.Opportunities AS opportunity
    WHERE opportunity.Status NOT IN (2, 3)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.SourceLinks AS link
          INNER JOIN @Items AS queued ON queued.Id = link.SourceItemId
          WHERE link.OpportunityId = opportunity.Id
      );

    COMMIT TRANSACTION;

    SELECT @OpportunitiesCount AS OpportunitiesPrepared,
           @ItemsCount AS SourceItemsQueued,
           @PromptVersionId AS RequestedPromptVersionId,
           (SELECT COUNT(*) FROM @Skipped) AS OpportunitiesNotQueued;
    SELECT Id, Title, Reason FROM @Skipped ORDER BY Id DESC;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
