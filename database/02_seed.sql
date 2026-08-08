-- Seed data for AI Brand Identity Studio
USE AiBrandStudio;
GO

DECLARE @RoleUser UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @RoleAdmin UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @AdminId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = N'User')
    INSERT INTO Roles (Id, Name) VALUES (@RoleUser, N'User');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = N'Admin')
    INSERT INTO Roles (Id, Name) VALUES (@RoleAdmin, N'Admin');

-- Password: Admin@12345 (BCrypt hash placeholder replaced at app seed time)
-- App EnsureSeedAsync will create admin if missing.

IF NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE Code = N'free')
INSERT INTO SubscriptionPlans (Id, Code, Name, MonthlyPriceInr, MonthlyCredits) VALUES
(NEWID(), N'free', N'Starter', 0, 50),
(NEWID(), N'builder', N'Builder', 1499, 300),
(NEWID(), N'scale', N'Scale', 4999, 1200);

IF NOT EXISTS (SELECT 1 FROM AiProviderConfigs WHERE ProviderName = N'Mock')
INSERT INTO AiProviderConfigs (Id, ProviderName, IsEnabled, ApiKeyMasked) VALUES
(NEWID(), N'Mock', 1, N'n/a'),
(NEWID(), N'OpenAI', 0, NULL),
(NEWID(), N'Gemini', 0, NULL),
(NEWID(), N'Ideogram', 0, NULL);

GO
