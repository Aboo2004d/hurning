using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public static class MembershipSchema
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<MembershipRuntimeState>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<MembershipRuntimeState>().HasData(new MembershipRuntimeState { Id = 1 });
        b.Entity<TelegramInbox>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<TelegramInbox>().HasIndex(x => new { x.ProcessedAt, x.LockedUntil });
        b.Entity<PaymentGatewaySetting>()
            .HasData(
                new PaymentGatewaySetting { Key = "Lahza" },
                new PaymentGatewaySetting { Key = "JawwalPay" }
            );
        b.Entity<MemberProfile>()
            .HasOne<IdentityUser>()
            .WithOne()
            .HasForeignKey<MemberProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberProfile>()
            .HasIndex(x => x.TelegramUserId)
            .IsUnique()
            .HasFilter("[TelegramUserId] IS NOT NULL");
        b.Entity<MemberProfile>().HasIndex(x => x.Mobile);
        b.Entity<MemberPreference>()
            .HasOne<MemberProfile>()
            .WithOne()
            .HasForeignKey<MemberPreference>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SavedOpportunity>().HasIndex(x => new { x.UserId, x.OpportunityId }).IsUnique();
        b.Entity<SavedOpportunity>()
            .HasOne(x => x.Opportunity)
            .WithMany()
            .HasForeignKey(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<SavedOpportunity>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpportunityAccessRule>().Property(x => x.Kind).ValueGeneratedNever();
        b.Entity<OpportunityAccessRule>()
            .HasData(
                Enum.GetValues<OpportunityKind>()
                    .Select(x => new OpportunityAccessRule { Kind = x, PublicDetails = true })
            );
        b.Entity<MembershipPlanVersion>()
            .HasOne<MembershipPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MembershipPlan>()
            .HasOne<MembershipPlanVersion>()
            .WithMany()
            .HasForeignKey(x => x.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberSubscription>()
            .HasOne(x => x.PlanVersion)
            .WithMany()
            .HasForeignKey(x => x.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberSubscription>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberSubscription>()
            .HasIndex(x => x.OrderId)
            .IsUnique()
            .HasFilter("[OrderId] IS NOT NULL");
        b.Entity<MemberSubscription>().HasIndex(x => new { x.UserId, x.EndsAt });
        b.Entity<MemberSubscription>()
            .HasOne<PaymentOrder>()
            .WithOne()
            .HasForeignKey<MemberSubscription>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<DiscountCode>().HasIndex(x => x.Code).IsUnique();
        b.Entity<DiscountCode>()
            .HasOne<MembershipPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentOrder>()
            .HasOne(x => x.PlanVersion)
            .WithMany()
            .HasForeignKey(x => x.PlanVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentOrder>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentOrder>()
            .HasOne<DiscountCode>()
            .WithMany()
            .HasForeignKey(x => x.DiscountCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentOrder>()
            .HasIndex(x => new { x.Gateway, x.ProviderReference })
            .IsUnique()
            .HasFilter("[ProviderReference] IS NOT NULL");
        b.Entity<PaymentOrder>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<PaymentReview>()
            .HasOne<PaymentOrder>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<PaymentReview>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<MemberChallenge>().HasIndex(x => x.LinkHash).IsUnique();
        b.Entity<MemberChallenge>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<MemberMessage>().HasIndex(x => new { x.UserId, x.EventKey }).IsUnique();
        b.Entity<MemberMessage>()
            .HasIndex(x => new
            {
                x.SentAt,
                x.CancelledAt,
                x.AvailableAt,
            });
        b.Entity<MemberMessage>()
            .HasOne<MemberProfile>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<OpportunityPublication>().Property(x => x.OpportunityId).ValueGeneratedNever();
        b.Entity<OpportunityPublication>().Property(x => x.AfterUserId).HasMaxLength(450);
        b.Entity<OpportunityPublication>()
            .HasOne(x => x.Opportunity)
            .WithOne()
            .HasForeignKey<OpportunityPublication>(x => x.OpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
