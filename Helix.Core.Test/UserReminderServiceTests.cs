﻿using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Helix.Domain.Data;
using Helix.Domain.Models;
using Helix.Services.Services;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Helix.UserReminderServices.Test
{
    public class UserReminderServiceTests : IAsyncLifetime
    {
        private HelixDbContext _dbContext;
        private IAppCache _appCache;

        public UserReminderServiceTests()
        {
            _appCache = new LazyCache.CachingService();
            _dbContext = CreateDbContext();
        }

        [Fact]
        public async Task AddReminderShouldAddValidReminderIfValid()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act
            var actual = await sut.AddUserReminderAsync(1, 2, 3, "Test Content", TimeSpan.FromDays(1));

            //Assert
            actual.Success.Should().Be(true);
        }

        [Fact]
        public async Task AddReminderShouldAddReminderToCache()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act
            var addedReminder = await sut.AddUserReminderAsync(1, 1, 1, "content", TimeSpan.FromHours(5));
            var actual = _appCache.Get<UserReminder>($"UserReminder:{addedReminder.Entity.Id}");

            //Assert
            actual.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteReminderShouldDeleteReminderFromCache()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act
            var addedReminder = await sut.AddUserReminderAsync(1, 1, 1, "as", TimeSpan.Zero);
            await sut.DeleteUserReminderAsync(1, 1, addedReminder.Entity.Id);
            var actual = _appCache.Get<UserReminder>($"UserReminder:{addedReminder.Entity.Id}");

            //Assert
            actual.Should().BeNull();
        }

        [Fact]
        public async Task AddReminderShouldReturnFailIfContentIsWhiteSpace()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act
            var actual = await sut.AddUserReminderAsync(1, 2, 3, "", TimeSpan.FromDays(1));

            //Assert
            actual.Success.Should().Be(false);
            actual.Errors.ErrorMessage.Should().Be("Reminder message can't be empty.");
        }

        [Fact]
        public async Task DeleteReminderShouldDeleteReminderIfValidId()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act

            var actual = await sut.AddUserReminderAsync(5, 1, 1, "content", TimeSpan.Zero);
            var expected = await sut.DeleteUserReminderAsync(5, 1, actual.Entity.Id);

            //Assert
            expected.Success.Should().Be(true);
        }

        [Fact]
        public async Task DeleteReminderShouldNotDeleteReminderIfInvalidUserId()
        {
            //Arrange
            var sut = new UserReminderService(_dbContext, _appCache);

            //Act

            var actual = await sut.AddUserReminderAsync(5, 1, 1, "content", TimeSpan.Zero);
            var expected = await sut.DeleteUserReminderAsync(1, 1, actual.Entity.Id);

            //Assert
            expected.Success.Should().Be(false);
            expected.Errors.ErrorMessage.Should().Be("This reminder does not exist, or does not belong to this user");
        }

        private HelixDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<HelixDbContext>().UseSqlite($"Data Source=UserReminderService{new Guid()}.db;");
            var dbContext = new HelixDbContext(optionsBuilder.Options);
            dbContext.Database.Migrate();
            return dbContext;
        }

        public Task InitializeAsync()
        {
            _dbContext = CreateDbContext();
            _appCache = new CachingService();
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            var dbName = _dbContext.Database.GetDbConnection().DataSource;
            _dbContext.Dispose();

            var fileExist = File.Exists(dbName);
            if (fileExist)
                File.Delete(dbName);

            return Task.CompletedTask;
        }
    }
}
