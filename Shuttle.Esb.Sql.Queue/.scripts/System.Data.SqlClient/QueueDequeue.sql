set xact_abort on

declare @HandleTransaction bit = 0

if (@@trancount = 0)
begin
	set @HandleTransaction = 1
	begin tran
end

update
	MachineQueue
set
	ManagedThreadId = ManagedThreadId
where
	MachineName = @MachineName
and
	QueueName = @QueueName

update
	[dbo].[{0}] 
set
	UnacknowledgedHash = @UnacknowledgedHash,
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

if (@HandleTransaction = 1)
begin
	commit tran
end
