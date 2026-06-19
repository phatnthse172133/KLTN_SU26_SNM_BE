using System;
using System.Linq;
using System.Threading.Tasks;
using DomainLayer.Entities;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var adminRoleId = Guid.Parse("a472e8e4-190b-4ddf-9c78-a68ead3a6ef5");
        var ownerRoleId = Guid.Parse("b361b8e4-280b-4ddf-9c78-b68ead3a6ef6");
        var customerRoleId = Guid.Parse("c250b8e4-370b-4ddf-9c78-c68ead3a6ef7");

        // 1. Seed Roles if they do not exist individually
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = adminRoleId,
                RoleName = "Admin",
                Description = "System Administrator role with full access",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Roles.AddAsync(adminRole);
            await context.SaveChangesAsync();
        }
        else
        {
            adminRoleId = adminRole.Id;
        }

        var ownerRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "BoothOwner");
        if (ownerRole == null)
        {
            ownerRole = new Role
            {
                Id = ownerRoleId,
                RoleName = "BoothOwner",
                Description = "Food Booth Owner role with management access",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Roles.AddAsync(ownerRole);
            await context.SaveChangesAsync();
        }
        else
        {
            ownerRoleId = ownerRole.Id;
        }

        var customerRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Customer");
        if (customerRole == null)
        {
            customerRole = new Role
            {
                Id = customerRoleId,
                RoleName = "Customer",
                Description = "Customer role with order and purchase access",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Roles.AddAsync(customerRole);
            await context.SaveChangesAsync();
        }
        else
        {
            customerRoleId = customerRole.Id;
        }

        // 2. Only seed the Admin account if the username "admin" does not exist
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.UserName == "admin");
        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = Guid.Parse("d149b8e4-460b-4ddf-9c78-d68ead3a6ef8"),
                RoleId = adminRoleId,
                UserName = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), // Plain password: Admin@123
                FullName = "System Administrator",
                Email = "admin@smartnightmarket.com",
                Phone = "0123456789",
                Address = "Tra Vinh Night Market",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();
        }
    }
}
