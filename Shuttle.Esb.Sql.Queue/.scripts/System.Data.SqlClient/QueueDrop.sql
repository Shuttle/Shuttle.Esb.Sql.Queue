IF  EXISTS (SELECT * FROM dbo.sysobjects WHERE id = OBJECT_ID(N'[{0}]') AND type = 'U')
DROP TABLE [dbo].[{0}]

IF OBJECT_ID (N'EndpointQueue', N'U') IS NOT NULL 
BEGIN
	declare @sql nvarchar(200)

	set @sql = 'delete from EndpointQueue where QueueName = ''{0}''';

	exec sp_executesql @sql
END