if (not exists (select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = 'dbo' and TABLE_NAME = '{0}'))
	return;

update
	[dbo].[{0}] 
set
	UnacknowledgedHash = null,
	UnacknowledgedDate = null,
	UnacknowledgedId = null
where 
	UnacknowledgedHash = @UnacknowledgedHash