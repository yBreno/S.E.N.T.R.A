-- CONFERENCIA SOMENTE LEITURA: executar antes e depois do arquivo principal.
-- Executar este arquivo inteiro; um unico result set, sem dados pessoais/credenciais.
SET NOCOUNT ON;
DECLARE @Expected TABLE (Position int, MigrationId nvarchar(150));
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

DECLARE @Applied TABLE (MigrationId nvarchar(150));
IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NOT NULL
    INSERT @Applied EXEC sys.sp_executesql N'SELECT MigrationId FROM dbo.__EFMigrationsHistory';
DECLARE @Count bigint=NULL;
IF OBJECT_ID(N'dbo.Users',N'U') IS NOT NULL
    EXEC sys.sp_executesql N'SELECT @Total=COUNT_BIG(*) FROM dbo.Users', N'@Total bigint OUTPUT', @Total=@Count OUTPUT;

SELECT DB_NAME() AS Banco, q.Grupo, q.Item, q.Resultado
FROM (
    SELECT 1 AS SortOrder, N'Migration' AS Grupo, e.MigrationId AS Item,
        CASE WHEN a.MigrationId IS NOT NULL THEN N'PRESENTE'
             WHEN e.Position<=5 THEN N'BASELINE AUSENTE - NAO PROSSEGUIR' ELSE N'PENDENTE' END AS Resultado
    FROM @Expected e LEFT JOIN @Applied a ON a.MigrationId=e.MigrationId
    UNION ALL SELECT 1,N'Migration',a.MigrationId,N'FORA DO ROTEIRO - NAO PROSSEGUIR'
      FROM @Applied a WHERE NOT EXISTS (SELECT 1 FROM @Expected e WHERE e.MigrationId=a.MigrationId)
    UNION ALL SELECT 2,N'Coluna',e.TableName+N'.'+e.ColumnName,CASE WHEN EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U')
        AND c.name=e.ColumnName AND TYPE_NAME(c.system_type_id)=e.TypeName
        AND c.max_length=e.MaxLength AND c.is_nullable=e.IsNullable
        AND c.is_computed=0 AND c.is_identity=0 AND (e.TypeName<>N'datetime2' OR c.scale=7)) THEN N'CONFERIDA' ELSE N'AUSENTE / DIVERGENTE' END FROM @Columns e
    UNION ALL SELECT 3,N'Indice',e.IndexName+N' / '+e.ColumnName,CASE WHEN EXISTS (SELECT 1 FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND i.name=e.IndexName
        AND i.is_unique=e.IsUnique AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
        AND i.type=2 AND ic.key_ordinal=e.KeyOrdinal AND c.name=e.ColumnName
        AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)
          =(SELECT COUNT(*) FROM @Indexes k WHERE k.TableName=e.TableName AND k.IndexName=e.IndexName)) THEN N'CONFERIDO' ELSE N'AUSENTE / DIVERGENTE' END FROM @Indexes e
    UNION ALL SELECT 4,N'FK',e.ConstraintName,CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys f
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
        JOIN sys.columns c ON c.object_id=f.parent_object_id AND c.column_id=fc.parent_column_id
        JOIN sys.columns r ON r.object_id=f.referenced_object_id AND r.column_id=fc.referenced_column_id
        WHERE f.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND f.name=e.ConstraintName
        AND f.referenced_object_id=OBJECT_ID(N'dbo.Users',N'U') AND c.name=e.ColumnName AND r.name=N'Id'
        AND f.delete_referential_action=0 AND f.update_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0
        AND (SELECT COUNT(*) FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)=1) THEN N'CONFERIDA' ELSE N'AUSENTE / DIVERGENTE' END FROM @ForeignKeys e
    UNION ALL SELECT 5,N'Check',e.ConstraintName,CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints c
        WHERE c.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND c.name=e.ConstraintName
        AND c.is_disabled=0 AND c.is_not_trusted=0) THEN N'CONFERIDO' ELSE N'AUSENTE / DIVERGENTE' END FROM @Checks e
    UNION ALL SELECT DISTINCT 6,N'PK',e.TableName,CASE WHEN EXISTS (SELECT 1 FROM sys.key_constraints k
        JOIN sys.index_columns ic ON ic.object_id=k.parent_object_id AND ic.index_id=k.unique_index_id
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE k.parent_object_id=OBJECT_ID(N'dbo.'+e.TableName,N'U') AND k.type=N'PK'
        AND ic.key_ordinal=1 AND c.name=CASE WHEN e.TableName=N'PasswordRecoveryLimits' THEN N'Key' ELSE N'Id' END
        AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=k.parent_object_id AND x.index_id=k.unique_index_id AND x.key_ordinal>0)=1) THEN N'CONFERIDA' ELSE N'AUSENTE / DIVERGENTE' END FROM @Columns e WHERE e.TableName<>N'Users'
    UNION ALL SELECT 7,N'Default',e.ColumnName,CASE WHEN EXISTS
      (SELECT 1 FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.Users',N'U') AND c.name=e.ColumnName AND c.default_object_id<>0)
      THEN N'PRESENTE' ELSE N'AUSENTE' END FROM @Columns e WHERE e.TableName=N'Users'
    UNION ALL SELECT 8,N'Metrica',N'Total de usuarios',COALESCE(CONVERT(nvarchar(100),@Count),N'TABELA AUSENTE')
    UNION ALL SELECT 8,N'Metrica',N'Transacoes abertas na sessao',CONVERT(nvarchar(100),@@TRANCOUNT)
) q ORDER BY q.SortOrder,q.Item;
