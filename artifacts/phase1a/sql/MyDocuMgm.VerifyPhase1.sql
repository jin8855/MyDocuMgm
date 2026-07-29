/*
  MyDocuMgm Phase 1 verification (SELECT only)
  Run only after selecting the existing MyDocuMgm database in SSMS.
*/
SELECT
    DB_NAME() AS CurrentDatabase,
    CASE WHEN DB_NAME() = N'MyDocuMgm' THEN 1 ELSE 0 END AS IsExpectedDatabase;

SELECT
    expected.TableName,
    CASE WHEN tables.object_id IS NOT NULL THEN 1 ELSE 0 END AS ExistsFlag
FROM (VALUES
    (N'Categories'), (N'Contents'), (N'ContentSteps'), (N'Tags'), (N'ContentTags'),
    (N'MediaAssets'), (N'SourceEvidence'), (N'PlaceDetails'), (N'CookingDetails'),
    (N'CookingIngredients'), (N'ExerciseDetails'), (N'CleaningLaundryDetails'),
    (N'TravelDetails'), (N'PhotoDetails'), (N'StudyDetails'), (N'ProductDetails'),
    (N'PhoneComputerDetails'), (N'TipDetails'), (N'OtherDetails'),
    (N'__EFMigrationsHistory')
) AS expected(TableName)
LEFT JOIN sys.tables AS tables
    ON tables.name = expected.TableName
   AND SCHEMA_NAME(tables.schema_id) = N'dbo'
ORDER BY expected.TableName;

SELECT
    Id,
    SortOrder,
    Code,
    DisplayName
FROM dbo.Categories
ORDER BY SortOrder;

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
WHERE indexes.is_hypothetical = 0
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
    tables.name AS TableName,
    columns.name AS ColumnName,
    types.name AS DataType,
    columns.max_length AS MaxLength,
    columns.is_nullable AS IsNullable
FROM sys.columns AS columns
INNER JOIN sys.tables AS tables ON tables.object_id = columns.object_id
INNER JOIN sys.types AS types ON types.user_type_id = columns.user_type_id
WHERE columns.name = N'RowVersion'
ORDER BY tables.name;
