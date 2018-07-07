declare @SequenceId int

select top 1 
	@SequenceId = SequenceId 
from 
	[dbo].[{0}] 
where 
	UnacknowledgedHash is null 
order by 
	SequenceId;

update
	[dbo].[{0}] 
set
	UnacknowledgedHash = @UnacknowledgedHash,
	UnacknowledgedDate = getdate()
where 
	SequenceId = @SequenceId;

select 
	SequenceId, 
	MessageId, 
	MessageBody 
from 
	[dbo].[{0}] 
where 
	SequenceId = @SequenceId;
