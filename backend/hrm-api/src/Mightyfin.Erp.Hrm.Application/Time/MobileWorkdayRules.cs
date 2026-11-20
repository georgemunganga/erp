namespace Mightyfin.Erp.Hrm.Application.Time;

public sealed record MobileWorkdayCommand(Guid RequestId,Guid AttendanceId,int ExpectedRevision,
    string Kind,string Reason="",string Summary="",string Blockers="");

/// <summary>Explicit employee actions only. Presence evidence and paid time are separate decisions.</summary>
public static class MobileWorkdayRules
{
    public static void Validate(MobileWorkdayCommand c)
    {
        if(c.RequestId==Guid.Empty || c.AttendanceId==Guid.Empty || c.ExpectedRevision<0
            || c.Kind is not ("break-start" or "break-end" or "departure" or "return" or "report")
            || !Text(c.Reason,300) || !Text(c.Summary,2000) || !Text(c.Blockers,1000))
            throw new DomainException("workday-invalid","Check the workday action and its details.");
        if(c.Kind=="report" ? c.Summary.Trim().Length<3 || c.Reason!="" : c.Summary!="" || c.Blockers!="")
            throw new DomainException("workday-invalid","A work report needs a summary; attendance actions cannot submit a report.");
        if(c.Kind=="departure" && c.Reason.Trim().Length<3)
            throw new DomainException("workday-reason-required","Explain the departure before continuing.");
    }
    private static bool Text(string? s,int maximum)=>s is not null && s.Length<=maximum
        && !s.Any(ch=>char.IsControl(ch) && ch is not ('\n' or '\r' or '\t'));

    public static string Apply(string current,MobileWorkdayCommand command,bool shiftClosed)
    {
        Validate(command);
        if(current is not ("working" or "on_break" or "away"))
            throw new DomainException("workday-needs-review","Ask HR to review the workday history.");
        if(command.Kind=="report")return current;
        if(shiftClosed)throw new DomainException("workday-closed","This shift is complete. You can still submit its work report.");
        return (current,command.Kind) switch {
            ("working","break-start")=>"on_break",
            ("on_break","break-end")=>"working",
            ("working","departure")=>"away",
            ("away","return")=>"working",
            _=>throw new DomainException("workday-transition","Refresh the workday before trying this action.")
        };
    }
}
