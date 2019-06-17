update
	[dbo].[{0}] 
set
	UnacknowledgedHash = null,
	UnacknowledgedDate = null,
	UnacknowledgedId = null
where 
	UnacknowledgedHash = @EndpointHash