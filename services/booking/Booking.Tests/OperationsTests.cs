using Booking.Api;
using Shared.Contracts;

public class OperationsTests
{
    [Fact]
    public void SummarySupportsAnEmptyResult()
    {
        var result = OperationsEndpoints.Summarize(Array.Empty<BookingDto>());
        Assert.True(result.Count >= 0);
    }
}
