-- Gerado pelo EF Core: somente incremento AddMaintenanceScheduling -> AddUserAdministration.
-- Revisar e executar MANUALMENTE em SQLCMD com -b ou SSMS em SQLCMD Mode.
-- Este arquivo nao foi executado no Azure. Interromper ao primeiro erro.
:ON ERROR EXIT
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
    THROW 51012, 'Historico EF ausente; destino ou baseline incorreto.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId]=N'20261023224842_AddMaintenanceScheduling')
    THROW 51013, 'Baseline AddMaintenanceScheduling obrigatorio antes deste incremento.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    IF EXISTS (SELECT 1 FROM [Users] WHERE [Role] NOT IN ('Admin','OperationManager','CentralOperator','MaintenanceTechnician','Agent'))
        THROW 51010, 'Cargos legados desconhecidos. Revise antes de atualizar; nenhuma conversao automatica sera feita.', 1;
    IF EXISTS (SELECT 1 FROM [Users]) AND NOT EXISTS (SELECT 1 FROM [Users] WHERE [Role]='Admin')
        THROW 51011, 'Existem contas sem administrador. Provisione um Admin por procedimento privado autorizado antes da migration.', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    ALTER TABLE [Users] ADD [AuthVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    ALTER TABLE [Users] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    ALTER TABLE [Users] ADD [Revision] bigint NOT NULL DEFAULT CAST(1 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    CREATE TABLE [UserAuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [Action] nvarchar(40) NOT NULL,
        [PreviousRole] nvarchar(40) NULL,
        [NewRole] nvarchar(40) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserAuditEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAuditEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAuditEvents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_AuthVersion] CHECK ([AuthVersion] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_Revision] CHECK ([Revision] > 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    CREATE INDEX [IX_UserAuditEvents_ActorUserId] ON [UserAuditEvents] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    CREATE INDEX [IX_UserAuditEvents_UserId_CreatedAt_Id] ON [UserAuditEvents] ([UserId], [CreatedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024223405_AddUserAdministration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024223405_AddUserAdministration', N'10.0.13');
END;

COMMIT;
GO

