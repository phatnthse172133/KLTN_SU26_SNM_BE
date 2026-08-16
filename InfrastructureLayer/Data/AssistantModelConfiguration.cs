using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data;

internal static class AssistantModelConfiguration
{
    public static void ConfigureAssistantModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssistantConversation>(entity =>
        {
            entity.ToTable("AssistantConversation");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(value => value.PendingUserMessage).HasMaxLength(2000);
            entity.Property(value => value.PendingParsedIntentJson).HasColumnType("jsonb");
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(value => value.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(value => new { value.CustomerId, value.CreatedAt }, "idx_assistantconversation_customer");
            entity.HasOne(value => value.Customer)
                .WithMany(value => value.AssistantConversations)
                .HasForeignKey(value => value.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Market)
                .WithMany()
                .HasForeignKey(value => value.MarketId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssistantMessage>(entity =>
        {
            entity.ToTable("AssistantMessage");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.Role).HasConversion<string>().HasMaxLength(20);
            entity.Property(value => value.Content).HasMaxLength(8000).IsRequired();
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(value => new { value.ConversationId, value.CreatedAt }, "idx_assistantmessage_conversation");
            entity.HasOne(value => value.Conversation)
                .WithMany(value => value.Messages)
                .HasForeignKey(value => value.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssistantMealPlan>(entity =>
        {
            entity.ToTable("AssistantMealPlan");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.EstimatedTotal).HasPrecision(12, 2);
            entity.Property(value => value.BudgetMax).HasPrecision(12, 2);
            entity.Property(value => value.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(value => new { value.ConversationId, value.CreatedAt }, "idx_assistantmealplan_conversation");
            entity.HasOne(value => value.Conversation)
                .WithMany(value => value.MealPlans)
                .HasForeignKey(value => value.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.NightMarket)
                .WithMany()
                .HasForeignKey(value => value.NightMarketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssistantMealPlanItem>(entity =>
        {
            entity.ToTable("AssistantMealPlanItem");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(value => value.UnitPriceSnapshot).HasPrecision(12, 2);
            entity.HasIndex(value => new { value.MealPlanId, value.DisplayOrder }, "idx_assistantmealplanitem_plan");
            entity.HasOne(value => value.MealPlan)
                .WithMany(value => value.Items)
                .HasForeignKey(value => value.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.FoodItem)
                .WithMany()
                .HasForeignKey(value => value.FoodItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
