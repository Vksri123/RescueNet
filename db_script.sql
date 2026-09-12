-- ==========================================
-- RescueNet Database Creation Script
-- Target DBMS: Microsoft SQL Server
-- ==========================================

-- Create Database (Uncomment if needed, or run against your existing DB)
-- CREATE DATABASE RescueNetDB;
-- GO
-- USE RescueNetDB;
-- GO

-- 1. Users Table
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        PhoneNumber NVARCHAR(20) NULL,
        Role NVARCHAR(50) NOT NULL, -- 'Citizen', 'RescueTeam', 'Admin'
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END

-- 2. Rescue Teams Table
IF OBJECT_ID('dbo.RescueTeams', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RescueTeams (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL UNIQUE, -- Links to Users table for authentication
        TeamName NVARCHAR(150) NOT NULL,
        ContactNumber NVARCHAR(20) NOT NULL,
        CurrentLatitude DECIMAL(18, 10) NULL,
        CurrentLongitude DECIMAL(18, 10) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Available', -- 'Available', 'Busy', 'Offline'
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_RescueTeams_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
END

-- 3. Emergency Requests Table
IF OBJECT_ID('dbo.EmergencyRequests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmergencyRequests (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CitizenId INT NOT NULL, -- Links to Users table (the Citizen who reported it)
        EmergencyType NVARCHAR(100) NOT NULL, -- 'Flood', 'Fire', 'Accident', 'Medical', 'Earthquake', etc.
        Description NVARCHAR(MAX) NOT NULL,
        Latitude DECIMAL(18, 10) NOT NULL,
        Longitude DECIMAL(18, 10) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Created', -- 'Created', 'AI Analyzed', 'Assigned', 'Rescue Team Accepted', 'On The Way', 'Rescue In Progress', 'Resolved'
        PriorityLevel NVARCHAR(50) NOT NULL DEFAULT 'Medium', -- 'Low', 'Medium', 'High', 'Critical'
        RiskScore INT NOT NULL DEFAULT 0, -- Calculated risk score (0-100)
        MediaUrl NVARCHAR(MAX) NULL, -- Path to uploaded image/video
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_EmergencyRequests_Users FOREIGN KEY (CitizenId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
    );
END

-- 4. Emergency Assignments Table
IF OBJECT_ID('dbo.EmergencyAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmergencyAssignments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EmergencyRequestId INT NOT NULL,
        RescueTeamId INT NOT NULL,
        AssignedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        AcceptedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Assigned', -- 'Assigned', 'Accepted', 'OnTheWay', 'InProgress', 'Completed', 'Rejected'
        CONSTRAINT FK_EmergencyAssignments_EmergencyRequests FOREIGN KEY (EmergencyRequestId) REFERENCES dbo.EmergencyRequests(Id) ON DELETE CASCADE,
        CONSTRAINT FK_EmergencyAssignments_RescueTeams FOREIGN KEY (RescueTeamId) REFERENCES dbo.RescueTeams(Id) ON DELETE NO ACTION
    );
END

-- 5. Emergency Status History Table
IF OBJECT_ID('dbo.EmergencyStatusHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmergencyStatusHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EmergencyRequestId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        ChangedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        ChangedByUserId INT NULL,
        CONSTRAINT FK_EmergencyStatusHistory_EmergencyRequests FOREIGN KEY (EmergencyRequestId) REFERENCES dbo.EmergencyRequests(Id) ON DELETE CASCADE,
        CONSTRAINT FK_EmergencyStatusHistory_Users FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION
    );
END
GO

-- 6. SMS Alerts Broadcast Log Table
IF OBJECT_ID('dbo.SMSAlerts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SMSAlerts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Message NVARCHAR(MAX) NOT NULL,
        TargetRole NVARCHAR(50) NOT NULL DEFAULT 'All', -- 'All', 'Citizen', 'RescueTeam'
        RecipientCount INT NOT NULL DEFAULT 0,
        SentAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        SentByUserId INT NULL,
        CONSTRAINT FK_SMSAlerts_Users FOREIGN KEY (SentByUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL
    );
END
GO

-- Seed Admin User (default login: admin / admin123)
-- PasswordHash is SHA256 of "admin123" -> 24075306B8BEF571B8B3D3F16A86CD8D121B66D606E88BC6FA2A77A55C8F7977
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, PhoneNumber, Role, CreatedAt)
    VALUES ('admin', '24075306B8BEF571B8B3D3F16A86CD8D121B66D606E88BC6FA2A77A55C8F7977', 'System Administrator', '911', 'Admin', GETDATE());
END
GO
