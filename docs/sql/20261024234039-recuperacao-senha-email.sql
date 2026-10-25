-- AZURE PORTAL QUERY EDITOR: copiar ESTE ARQUIVO INTEIRO e executar uma unica vez por tentativa.
-- Sem GO, diretivas de cliente, USE, credenciais ou conexao externa.
-- Antes/depois: executar 20261024234039-conferencia-query-editor.sql (somente leitura).
-- Revisar destino/backup e suspender escritas da aplicacao. Nao publicar a API antes do schema.
-- As duas migrations novas e seus registros de historico compartilham UMA transacao.
-- Repeticao valida schema; erro reverte somente esta tentativa, sem apagar dados preexistentes.
SET NOCOUNT ON;
IF @@TRANCOUNT<>0
    THROW 51000, 'Execute fora de qualquer transacao existente; nao fracionar este lote.', 1;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @Expected TABLE (Position int PRIMARY KEY, MigrationId nvarchar(150) NOT NULL);
INSERT @Expected VALUES
    (1, N'20261018170813_InitialCreate'),
    (2, N'20261018220253_RemoveOrganization'),
    (3, N'20261019031247_AddPredictiveCity'),
    (4, N'20261023214553_AddEquipmentCategory'),
    (5, N'20261023224842_AddMaintenanceScheduling'),
    (6, N'20261024223405_AddUserAdministration'),
    (7, N'20261024234039_AddPasswordRecovery');
DECLARE @Columns TABLE (TableName sysname, ColumnName sysname, TypeName sysname, MaxLength smallint, IsNullable bit);
INSERT @Columns VALUES
    (N'Users', N'AuthVersion', N'bigint', 8, 0),
    (N'Users', N'IsActive', N'bit', 1, 0),
    (N'Users', N'Revision', N'bigint', 8, 0),
    (N'UserAuditEvents', N'Id', N'uniqueidentifier', 16, 0),
    (N'UserAuditEvents', N'UserId', N'uniqueidentifier', 16, 0),
    (N'UserAuditEvents', N'ActorUserId', N'uniqueidentifier', 16, 1),
    (N'UserAuditEvents', N'Action', N'nvarchar', 80, 0),
    (N'UserAuditEvents', N'PreviousRole', N'nvarchar', 80, 1),
    (N'UserAuditEvents', N'NewRole', N'nvarchar', 80, 1),
    (N'UserAuditEvents', N'CreatedAt', N'datetime2', 8, 0),
    (N'PasswordRecoveryLimits', N'Key', N'varchar', 64, 0),
    (N'PasswordRecoveryLimits', N'ExpiresAt', N'datetime2', 8, 0),
    (N'PasswordRecoveryLimits', N'Count', N'int', 4, 0),
    (N'PasswordResetTokens', N'Id', N'uniqueidentifier', 16, 0),
    (N'PasswordResetTokens', N'UserId', N'uniqueidentifier', 16, 0),
    (N'PasswordResetTokens', N'TokenHash', N'varchar', 64, 0),
    (N'PasswordResetTokens', N'AuthVersion', N'bigint', 8, 0),
    (N'PasswordResetTokens', N'ExpiresAt', N'datetime2', 8, 0),
    (N'PasswordResetTokens', N'Delivered', N'bit', 1, 0),
    (N'PasswordResetTokens', N'InvalidatedAt', N'datetime2', 8, 1),
    (N'PasswordResetTokens', N'CreatedAt', N'datetime2', 8, 0);
DECLARE @Indexes TABLE (TableName sysname, IndexName sysname, ColumnName sysname, KeyOrdinal int, IsUnique bit);
INSERT @Indexes VALUES
    (N'UserAuditEvents', N'IX_UserAuditEvents_ActorUserId', N'ActorUserId', 1, 0),
    (N'UserAuditEvents', N'IX_UserAuditEvents_UserId_CreatedAt_Id', N'UserId', 1, 0),
    (N'UserAuditEvents', N'IX_UserAuditEvents_UserId_CreatedAt_Id', N'CreatedAt', 2, 0),
    (N'UserAuditEvents', N'IX_UserAuditEvents_UserId_CreatedAt_Id', N'Id', 3, 0),
    (N'PasswordRecoveryLimits', N'IX_PasswordRecoveryLimits_ExpiresAt', N'ExpiresAt', 1, 0),
    (N'PasswordResetTokens', N'IX_PasswordResetTokens_TokenHash', N'TokenHash', 1, 1),
    (N'PasswordResetTokens', N'IX_PasswordResetTokens_UserId', N'UserId', 1, 1);
DECLARE @ForeignKeys TABLE (TableName sysname, ConstraintName sysname, ColumnName sysname);
INSERT @ForeignKeys VALUES
    (N'UserAuditEvents', N'FK_UserAuditEvents_Users_ActorUserId', N'ActorUserId'),
    (N'UserAuditEvents', N'FK_UserAuditEvents_Users_UserId', N'UserId'),
    (N'PasswordResetTokens', N'FK_PasswordResetTokens_Users_UserId', N'UserId');
DECLARE @Checks TABLE (TableName sysname, ConstraintName sysname);
INSERT @Checks VALUES
    (N'Users', N'CK_Users_AuthVersion'),
    (N'Users', N'CK_Users_Revision'),
    (N'PasswordResetTokens', N'CK_PasswordResetTokens_AuthVersion');

DECLARE @Applied TABLE (MigrationId nvarchar(150) PRIMARY KEY);
DECLARE @UsersBefore bigint, @UsersAfter bigint, @LockResult int;
DECLARE @AddedAdministration bit=0, @AddedRecovery bit=0;
BEGIN TRY
    BEGIN TRANSACTION;
    -- Mesmo recurso usado pela administracao de contas; duas execucoes cooperantes nao concorrem.
    EXEC @LockResult=sys.sp_getapplock @Resource=N'SENTRA.UserAdministration.v1',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
    IF @LockResult<0 THROW 51001, 'Bloqueio de administracao ocupado; aguarde e confira antes de repetir.', 1;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL
        THROW 51012, 'Historico EF ausente. Banco/schema incorreto; nao cria baseline automaticamente.', 1;
    IF OBJECT_ID(N'dbo.Users',N'U') IS NULL
        THROW 51014, 'Tabela dbo.Users ausente. Banco/schema incorreto.', 1;
    INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory';
    IF EXISTS (SELECT 1 FROM @Expected e WHERE e.Position<=5 AND NOT EXISTS
        (SELECT 1 FROM @Applied a WHERE a.MigrationId=e.MigrationId))
        THROW 51013, 'Falta uma das CINCO migrations anteriores. Execute a consulta de conferencia; nao prossiga.', 1;
    IF EXISTS (SELECT 1 FROM @Applied a WHERE NOT EXISTS (SELECT 1 FROM @Expected e WHERE e.MigrationId=a.MigrationId))
        THROW 51015, 'Historico contem migration fora deste roteiro. Revisar a versao do banco antes de prosseguir.', 1;
    IF EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024234039_AddPasswordRecovery')
        AND NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024223405_AddUserAdministration')
        THROW 51016, 'Historico fora de ordem: recuperacao exige administracao de usuarios.', 1;
    EXEC sys.sp_executesql N'SELECT @Count=COUNT_BIG(*) FROM dbo.Users WITH (TABLOCKX,HOLDLOCK)',
        N'@Count bigint OUTPUT', @Count=@UsersBefore OUTPUT;

    -- MIGRATION 6: AddUserAdministration (DDL equivalente a migration EF preservada).
    IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024223405_AddUserAdministration')
    BEGIN
        IF COL_LENGTH(N'dbo.Users',N'AuthVersion') IS NOT NULL OR COL_LENGTH(N'dbo.Users',N'IsActive') IS NOT NULL
            OR COL_LENGTH(N'dbo.Users',N'Revision') IS NOT NULL OR OBJECT_ID(N'dbo.UserAuditEvents') IS NOT NULL
            THROW 51017, 'Schema administrativo parcial sem historico. Revisao humana necessaria; nada sera adotado/apagado.', 1;
        EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Role] NOT IN (''Admin'',''OperationManager'',''CentralOperator'',''MaintenanceTechnician'',''Agent''))
        THROW 51010, ''Cargos legados desconhecidos. Revise antes de atualizar; nenhuma conversao automatica sera feita.'', 1;
    IF EXISTS (SELECT 1 FROM [dbo].[Users]) AND NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Role]=''Admin'')
        THROW 51011, ''Existem contas sem administrador. Provisione um Admin por procedimento privado autorizado antes da migration.'', 1;';
        EXEC sys.sp_executesql N'ALTER TABLE [dbo].[Users] ADD [AuthVersion] bigint NOT NULL DEFAULT CAST(1 AS bigint);';
        EXEC sys.sp_executesql N'ALTER TABLE [dbo].[Users] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);';
        EXEC sys.sp_executesql N'ALTER TABLE [dbo].[Users] ADD [Revision] bigint NOT NULL DEFAULT CAST(1 AS bigint);';
        EXEC sys.sp_executesql N'CREATE TABLE [dbo].[UserAuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [Action] nvarchar(40) NOT NULL,
        [PreviousRole] nvarchar(40) NULL,
        [NewRole] nvarchar(40) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserAuditEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserAuditEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserAuditEvents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );';
        EXEC sys.sp_executesql N'EXEC(N''ALTER TABLE [dbo].[Users] ADD CONSTRAINT [CK_Users_AuthVersion] CHECK ([AuthVersion] > 0)'');';
        EXEC sys.sp_executesql N'EXEC(N''ALTER TABLE [dbo].[Users] ADD CONSTRAINT [CK_Users_Revision] CHECK ([Revision] > 0)'');';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_UserAuditEvents_ActorUserId] ON [dbo].[UserAuditEvents] ([ActorUserId]);';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_UserAuditEvents_UserId_CreatedAt_Id] ON [dbo].[UserAuditEvents] ([UserId], [CreatedAt], [Id]);';
    END;
    -- Verifica o schema mesmo quando a migration ja consta no historico; nao adota drift.
    IF EXISTS (SELECT 1 FROM @Columns e WHERE e.TableName IN (N'Users',N'UserAuditEvents') AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U')
            AND c.name=e.ColumnName AND TYPE_NAME(c.system_type_id)=e.TypeName
            AND c.max_length=e.MaxLength AND c.is_nullable=e.IsNullable
            AND c.is_computed=0 AND c.is_identity=0 AND (e.TypeName<>N'datetime2' OR c.scale=7)))
        THROW 51020, 'Colunas ausentes ou divergentes; conferir consulta separada. Nenhum reparo automatico.', 1;
    IF EXISTS (SELECT 1 FROM @Indexes e WHERE e.TableName IN (N'Users',N'UserAuditEvents') AND NOT EXISTS (SELECT 1 FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND i.name=e.IndexName
            AND i.is_unique=e.IsUnique AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
            AND i.type=2 AND ic.key_ordinal=e.KeyOrdinal AND c.name=e.ColumnName
            AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)
              =(SELECT COUNT(*) FROM @Indexes k WHERE k.TableName=e.TableName AND k.IndexName=e.IndexName)))
        THROW 51021, 'Indices ausentes, desabilitados ou divergentes.', 1;
    IF EXISTS (SELECT 1 FROM @ForeignKeys e WHERE e.TableName IN (N'Users',N'UserAuditEvents') AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys f
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
            JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=fc.parent_column_id
            JOIN sys.columns r ON r.object_id=f.referenced_object_id AND r.column_id=fc.referenced_column_id
            WHERE f.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND f.name=e.ConstraintName
            AND f.referenced_object_id=OBJECT_ID(N'dbo.Users',N'U') AND c.name=e.ColumnName AND r.name=N'Id'
            AND f.delete_referential_action=0 AND f.update_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)=1))
        THROW 51022, 'Chaves estrangeiras ausentes, divergentes ou sem validacao.', 1;
    IF EXISTS (SELECT 1 FROM @Checks e WHERE e.TableName IN (N'Users',N'UserAuditEvents') AND NOT EXISTS (SELECT 1 FROM sys.check_constraints c
            WHERE c.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND c.name=e.ConstraintName
            AND c.is_disabled=0 AND c.is_not_trusted=0))
        THROW 51023, 'Checks ausentes, desabilitados ou sem validacao.', 1;
    IF EXISTS (SELECT DISTINCT e.TableName FROM @Columns e WHERE e.TableName=N'UserAuditEvents' AND NOT EXISTS (SELECT 1 FROM sys.key_constraints k
            JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE k.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND k.type=N'PK'
            AND ic.key_ordinal=1 AND c.name=CASE WHEN e.TableName=N'PasswordRecoveryLimits' THEN N'Key' ELSE N'Id' END
            AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=k.parent_object_id AND x.index_id=k.unique_index_id AND x.key_ordinal>0)=1))
        THROW 51024, 'Chave primaria ausente ou divergente.', 1;
    IF EXISTS (SELECT 1 FROM @Columns e WHERE e.TableName=N'Users' AND NOT EXISTS
        (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Users',N'U') AND c.name=e.ColumnName AND c.default_object_id<>0))
        THROW 51025, 'Defaults das novas colunas de Users ausentes.', 1;
    -- Historico somente DEPOIS do DDL e das verificacoes; permanece na mesma transacao.
    IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024223405_AddUserAdministration')
    BEGIN
        EXEC sys.sp_executesql N'INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N''20261024223405_AddUserAdministration'',N''10.0.13'')';
        INSERT @Applied VALUES (N'20261024223405_AddUserAdministration');
        SET @AddedAdministration=1;
    END;

    -- MIGRATION 7: AddPasswordRecovery, obrigatoriamente apos a 6.
    IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024234039_AddPasswordRecovery')
    BEGIN
        IF OBJECT_ID(N'dbo.PasswordRecoveryLimits') IS NOT NULL OR OBJECT_ID(N'dbo.PasswordResetTokens') IS NOT NULL
            THROW 51018, 'Schema de recuperacao parcial sem historico. Revisao humana necessaria.', 1;
        EXEC sys.sp_executesql N'CREATE TABLE [dbo].[PasswordRecoveryLimits] (
        [Key] varchar(64) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Count] int NOT NULL,
        CONSTRAINT [PK_PasswordRecoveryLimits] PRIMARY KEY ([Key])
    );';
        EXEC sys.sp_executesql N'CREATE TABLE [dbo].[PasswordResetTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] varchar(64) NOT NULL,
        [AuthVersion] bigint NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Delivered] bit NOT NULL,
        [InvalidatedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PasswordResetTokens_AuthVersion] CHECK ([AuthVersion] > 0),
        CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );';
        EXEC sys.sp_executesql N'CREATE INDEX [IX_PasswordRecoveryLimits_ExpiresAt] ON [dbo].[PasswordRecoveryLimits] ([ExpiresAt]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_PasswordResetTokens_TokenHash] ON [dbo].[PasswordResetTokens] ([TokenHash]);';
        EXEC sys.sp_executesql N'CREATE UNIQUE INDEX [IX_PasswordResetTokens_UserId] ON [dbo].[PasswordResetTokens] ([UserId]);';
    END;
    -- Verifica o schema mesmo quando a migration ja consta no historico; nao adota drift.
    IF EXISTS (SELECT 1 FROM @Columns e WHERE e.TableName IN (N'PasswordRecoveryLimits',N'PasswordResetTokens') AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U')
            AND c.name=e.ColumnName AND TYPE_NAME(c.system_type_id)=e.TypeName
            AND c.max_length=e.MaxLength AND c.is_nullable=e.IsNullable
            AND c.is_computed=0 AND c.is_identity=0 AND (e.TypeName<>N'datetime2' OR c.scale=7)))
        THROW 51020, 'Colunas ausentes ou divergentes; conferir consulta separada. Nenhum reparo automatico.', 1;
    IF EXISTS (SELECT 1 FROM @Indexes e WHERE e.TableName IN (N'PasswordRecoveryLimits',N'PasswordResetTokens') AND NOT EXISTS (SELECT 1 FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE i.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND i.name=e.IndexName
            AND i.is_unique=e.IsUnique AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
            AND i.type=2 AND ic.key_ordinal=e.KeyOrdinal AND c.name=e.ColumnName
            AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)
              =(SELECT COUNT(*) FROM @Indexes k WHERE k.TableName=e.TableName AND k.IndexName=e.IndexName)))
        THROW 51021, 'Indices ausentes, desabilitados ou divergentes.', 1;
    IF EXISTS (SELECT 1 FROM @ForeignKeys e WHERE e.TableName IN (N'PasswordRecoveryLimits',N'PasswordResetTokens') AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys f
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
            JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=fc.parent_column_id
            JOIN sys.columns r ON r.object_id=f.referenced_object_id AND r.column_id=fc.referenced_column_id
            WHERE f.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND f.name=e.ConstraintName
            AND f.referenced_object_id=OBJECT_ID(N'dbo.Users',N'U') AND c.name=e.ColumnName AND r.name=N'Id'
            AND f.delete_referential_action=0 AND f.update_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0
            AND (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)=1))
        THROW 51022, 'Chaves estrangeiras ausentes, divergentes ou sem validacao.', 1;
    IF EXISTS (SELECT 1 FROM @Checks e WHERE e.TableName IN (N'PasswordRecoveryLimits',N'PasswordResetTokens') AND NOT EXISTS (SELECT 1 FROM sys.check_constraints c
            WHERE c.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND c.name=e.ConstraintName
            AND c.is_disabled=0 AND c.is_not_trusted=0))
        THROW 51023, 'Checks ausentes, desabilitados ou sem validacao.', 1;
    IF EXISTS (SELECT DISTINCT e.TableName FROM @Columns e WHERE e.TableName IN (N'PasswordRecoveryLimits',N'PasswordResetTokens') AND NOT EXISTS (SELECT 1 FROM sys.key_constraints k
            JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            WHERE k.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND k.type=N'PK'
            AND ic.key_ordinal=1 AND c.name=CASE WHEN e.TableName=N'PasswordRecoveryLimits' THEN N'Key' ELSE N'Id' END
            AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=k.parent_object_id AND x.index_id=k.unique_index_id AND x.key_ordinal>0)=1))
        THROW 51024, 'Chave primaria ausente ou divergente.', 1;
    IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261024234039_AddPasswordRecovery')
    BEGIN
        EXEC sys.sp_executesql N'INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N''20261024234039_AddPasswordRecovery'',N''10.0.13'')';
        INSERT @Applied VALUES (N'20261024234039_AddPasswordRecovery');
        SET @AddedRecovery=1;
    END;
    EXEC sys.sp_executesql N'SELECT @Count=COUNT_BIG(*) FROM dbo.Users', N'@Count bigint OUTPUT', @Count=@UsersAfter OUTPUT;
    IF @UsersAfter<>@UsersBefore THROW 51026, 'Quantidade de usuarios mudou nesta tentativa. Operacao revertida.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Unico result set final; nenhuma senha/hash/token/dado pessoal e exibido.
SELECT DB_NAME() AS Banco, e.Position AS Ordem, e.MigrationId,
    CASE WHEN e.Position=6 AND @AddedAdministration=1 OR e.Position=7 AND @AddedRecovery=1
         THEN N'APLICADA NESTA EXECUCAO' ELSE N'JA APLICADA / CONFERIDA' END AS Resultado,
    @UsersBefore AS UsuariosAntes, @UsersAfter AS UsuariosDepois, @@TRANCOUNT AS TransacoesAbertas
FROM @Expected e ORDER BY e.Position;
