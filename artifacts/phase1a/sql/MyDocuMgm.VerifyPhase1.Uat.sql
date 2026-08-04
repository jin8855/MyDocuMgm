/*
  MyDocuMgm Phase 1 disposable UAT verification candidate (SELECT only after guard).
  Only an isolated database whose name exactly matches
  MyDocuMgm_UAT_<14 digits>_<8 hexadecimal characters> may continue.
  This candidate is not an approved execution authority.
*/
DECLARE @UatDatabaseName sysname = DB_NAME();
IF @UatDatabaseName IS NULL
   OR LEN(@UatDatabaseName) <> 37
   OR DATALENGTH(@UatDatabaseName) <> 74
   OR LEFT(@UatDatabaseName, 14) COLLATE Latin1_General_100_BIN2
        <> N'MyDocuMgm_UAT_' COLLATE Latin1_General_100_BIN2
   OR SUBSTRING(@UatDatabaseName, 15, 14) COLLATE Latin1_General_100_BIN2
        LIKE N'%[^0-9]%' COLLATE Latin1_General_100_BIN2
   OR SUBSTRING(@UatDatabaseName, 29, 1) COLLATE Latin1_General_100_BIN2
        <> N'_' COLLATE Latin1_General_100_BIN2
   OR SUBSTRING(@UatDatabaseName, 30, 8) COLLATE Latin1_General_100_BIN2
        LIKE N'%[^0-9A-Fa-f]%' COLLATE Latin1_General_100_BIN2
    THROW 51000, N'WRONG_TARGET_DATABASE: this SQL is only for an isolated MyDocuMgm_UAT_<14 digits>_<8 hex> database.', 1;

SELECT
    DB_NAME() AS CurrentDatabase,
    CAST(1 AS bit) AS IsExpectedDatabase,
    N'VERIFICATION_CONTINUED' AS VerificationGate;

SELECT
    expected.TableName,
    CASE WHEN tables.object_id IS NOT NULL THEN 1 ELSE 0 END AS ExistsFlag
FROM (VALUES
    (N'Categories'), (N'CategorySearchAttributes'), (N'Contents'), (N'ContentSteps'),
    (N'Tags'), (N'ContentTags'), (N'MediaAssets'), (N'SourceEvidence'),
    (N'PlaceDetails'), (N'CookingDetails'), (N'CookingIngredients'),
    (N'ExerciseDetails'), (N'CleaningLaundryDetails'), (N'TravelDetails'),
    (N'PhotoDetails'), (N'StudyDetails'), (N'ProductDetails'),
    (N'PhoneComputerDetails'), (N'TipDetails'), (N'OtherDetails'),
    (N'__EFMigrationsHistory')
) AS expected(TableName)
LEFT JOIN sys.tables AS tables
    ON tables.name = expected.TableName
   AND SCHEMA_NAME(tables.schema_id) = N'dbo'
ORDER BY expected.TableName;

SELECT
    tables.name AS TableName,
    columns.name AS ColumnName,
    types.name AS DataType,
    columns.max_length AS MaxLength,
    columns.is_nullable AS IsNullable,
    columns.is_identity AS IsIdentity
FROM sys.columns AS columns
INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id
INNER JOIN sys.types AS types ON types.user_type_id = columns.user_type_id
WHERE SCHEMA_NAME(tables.schema_id) = N'dbo'
  AND (
      columns.name IN (
          N'CurrentWorkflowStep', N'IngredientType', N'IsPrimary',
          N'SourceTimestampMs', N'IsSelected', N'MediaAssetId', N'RowVersion')
      OR tables.name = N'CategorySearchAttributes'
  )
ORDER BY tables.name, columns.column_id;

SELECT
    tables.name AS TableName,
    indexes.name AS IndexName,
    indexes.is_unique AS IsUnique,
    STRING_AGG(columns.name, N', ') WITHIN GROUP (ORDER BY index_columns.key_ordinal) AS Columns
FROM sys.indexes AS indexes
INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
INNER JOIN sys.index_columns AS index_columns
    ON index_columns.object_id = indexes.object_id
   AND index_columns.index_id = indexes.index_id
INNER JOIN sys.columns AS columns
    ON columns.object_id = index_columns.object_id
   AND columns.column_id = index_columns.column_id
WHERE SCHEMA_NAME(tables.schema_id) = N'dbo'
  AND indexes.is_hypothetical = 0
  AND indexes.name IS NOT NULL
GROUP BY tables.name, indexes.name, indexes.is_unique
ORDER BY tables.name, indexes.name;

SELECT
    OBJECT_NAME(parent_object_id) AS TableName,
    name AS CheckConstraintName,
    definition AS CheckDefinition
FROM sys.check_constraints
WHERE OBJECT_SCHEMA_NAME(parent_object_id) = N'dbo'
ORDER BY TableName, CheckConstraintName;

SELECT
    OBJECT_NAME(foreign_keys.parent_object_id) AS ChildTable,
    foreign_keys.name AS ForeignKeyName,
    OBJECT_NAME(foreign_keys.referenced_object_id) AS ParentTable,
    foreign_keys.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys AS foreign_keys
WHERE OBJECT_SCHEMA_NAME(foreign_keys.parent_object_id) = N'dbo'
ORDER BY ChildTable, ForeignKeyName;

SELECT
    history_table.name AS TableName,
    migration_id.name AS MigrationIdColumn,
    product_version.name AS ProductVersionColumn
FROM sys.tables AS history_table
INNER JOIN sys.columns AS migration_id
    ON migration_id.object_id = history_table.object_id
   AND migration_id.name = N'MigrationId'
INNER JOIN sys.columns AS product_version
    ON product_version.object_id = history_table.object_id
   AND product_version.name = N'ProductVersion'
WHERE SCHEMA_NAME(history_table.schema_id) = N'dbo'
  AND history_table.name = N'__EFMigrationsHistory';
