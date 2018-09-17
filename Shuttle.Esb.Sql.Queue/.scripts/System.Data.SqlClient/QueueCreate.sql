IF OBJECT_ID (N'{0}', N'U') IS NULL 
	CREATE TABLE [dbo].[{0}](
		[SequenceId] [int] IDENTITY(1,1) NOT NULL,
		[MessageId] [uniqueidentifier] NOT NULL,
		[MessageBody] [varbinary](max) NOT NULL,
		[UnacknowledgedHash] binary(16) NULL,
		[UnacknowledgedDate] datetime NULL,
		[UnacknowledgedId] [uniqueidentifier] NULL,
	CONSTRAINT [PK_{0}] PRIMARY KEY CLUSTERED 
	(
		[SequenceId] ASC
	) ON [PRIMARY]
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

IF COL_LENGTH('{0}', 'UnacknowledgedHash') IS NULL
	ALTER TABLE dbo.[{0}] ADD UnacknowledgedHash binary(16) NULL

IF COL_LENGTH('{0}', 'UnacknowledgedDate') IS NULL
	ALTER TABLE dbo.[{0}] ADD UnacknowledgedDate datetime NULL

IF COL_LENGTH('{0}', 'UnacknowledgedId') IS NULL
	ALTER TABLE dbo.[{0}] ADD UnacknowledgedId uniqueidentifier NULL

IF IndexProperty(Object_Id('{0}'), 'IX_{0}_UnacknowledgedId', 'IndexId') IS NULL
BEGIN
    CREATE NONCLUSTERED INDEX 
        [IX_{0}_UnacknowledgedId]
    ON 
        [{0}]
        (
            UnacknowledgedId
        ) 
    WITH
        ( 
            STATISTICS_NORECOMPUTE = OFF, 
            IGNORE_DUP_KEY = OFF, 
            ALLOW_ROW_LOCKS = ON, 
            ALLOW_PAGE_LOCKS = ON
        ) 
    ON 
        [PRIMARY]
END
