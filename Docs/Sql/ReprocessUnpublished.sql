-- Local test: connect to your LOCAL SQL Server instance first.
-- USE selects the database on that connection; it does not switch servers.
-- Update the app and collector before starting the next collection round.
-- Requeues all source items not shared with published/archived opportunities.
-- Draft/review/hidden opportunities in that batch lose manual override and are re-extracted.
-- Source content, revision history, original links and social publication history are retained.
USE [hiring];
GO

SELECT DB_NAME() AS DatabaseName, CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName;
GO

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

    DECLARE @UpdatedOpportunities TABLE (Id bigint PRIMARY KEY);
    DECLARE @UpdatedItems TABLE (Id bigint PRIMARY KEY);

    UPDATE opportunity
    SET Status = 0, -- Draft: temporary until the new extraction is applied
        AdminOverride = 0
    OUTPUT inserted.Id INTO @UpdatedOpportunities (Id)
    FROM dbo.Opportunities AS opportunity
    WHERE opportunity.Status NOT IN (2, 3)
      AND EXISTS (
          SELECT 1
          FROM dbo.SourceLinks AS link
          INNER JOIN @Items AS queued ON queued.Id = link.SourceItemId
          WHERE link.OpportunityId = opportunity.Id
      );

    UPDATE item
    SET Status = 0, -- Discovered: fetch original source before AI
        Attempts = 0,
        RetryAt = NULL,
        LastVerifiedAt = NULL,
        Error = N'',
        ExtractionJson = N'',
        ExtractionPromptVersionId = NULL,
        RequestedPromptVersionId = @PromptVersionId
    OUTPUT inserted.Id INTO @UpdatedItems (Id)
    FROM dbo.SourceItems AS item
    INNER JOIN @Items AS queued ON queued.Id = item.Id;

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

    -- Count the actual OUTPUT rows; independent of rowcount state after connection recovery.
    SELECT (SELECT COUNT(*) FROM @UpdatedOpportunities) AS OpportunitiesPrepared,
           (SELECT COUNT(*) FROM @UpdatedItems) AS SourceItemsQueued,
           @PromptVersionId AS RequestedPromptVersionId,
           (SELECT COUNT(*) FROM @Skipped) AS OpportunitiesNotQueued;
    SELECT Id, Title, Reason FROM @Skipped ORDER BY Id DESC;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
