using Shared.Contracts;
namespace Booking.Domain;
public static class BookingRules
{
    public static bool CanTransition(BookingStatus current,BookingStatus next) => (current,next) switch
    {
        (BookingStatus.PendingApproval,BookingStatus.Approved or BookingStatus.Rejected or BookingStatus.Cancelled)=>true,
        (BookingStatus.Approved,BookingStatus.Assigned or BookingStatus.Cancelled)=>true,
        (BookingStatus.Assigned,BookingStatus.Completed or BookingStatus.Cancelled)=>true,
        _=>false
    };
    public static bool CanApprove(Guid actor,string[] roles,Guid? employeeManager) => roles.Contains(Roles.Manager)&&actor==employeeManager;
    public static bool CanCancel(Guid actor,string[] roles,BookingDto booking) => actor==booking.EmployeeId || (roles.Contains(Roles.Manager)&&actor==booking.ManagerId) || roles.Contains(Roles.Dispatcher);
}
public static class OperationalReports
{
    public static bool InDispatchQueue(BookingStatus status) => status switch
    {
        BookingStatus.PendingApproval or BookingStatus.Rejected or BookingStatus.Completed or BookingStatus.Cancelled => false,
        _ => true
    };
}
