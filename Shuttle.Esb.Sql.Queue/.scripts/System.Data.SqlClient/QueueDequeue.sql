update
	EndpointQueue
set
	EndpointHash = @EndpointHash
where
	EndpointHash = @EndpointHash

update
	[dbo].[{0}] 
set
	UnacknowledgedHash = @EndpointHash,
	UnacknowledgedDate = getdate(),
	UnacknowledgedId = @UnacknowledgedId
where 
	SequenceId = 
	(
		select top 1 
			SequenceId 
		from 
			[dbo].[{0}] 
		where 
			UnacknowledgedHash is null 
		order by 
			SequenceId
	);

select 
	SequenceId, 
	MessageId, 
	MessageBody 
from 
	[dbo].[{0}] 
where 
	UnacknowledgedId = @UnacknowledgedId;

