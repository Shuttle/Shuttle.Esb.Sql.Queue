update
	[dbo].[{0}] 
set
	UnacknowledgedHash = null,
	UnacknowledgedDate = null
where 
	UnacknowledgedHash = @UnacknowledgedHash