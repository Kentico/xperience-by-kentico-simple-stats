using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OrderStatusTonesTests
{
    // Default statuses of a new project.
    [TestCase("Pending", "Pending", StatsTone.Caution)]
    [TestCase("PaymentFailed", "Payment failed", StatsTone.Problem)]
    [TestCase("PaymentReceived", "Payment received", StatsTone.Done)]
    [TestCase("Processing", "Processing", StatsTone.Neutral)]
    [TestCase("Fulfilled", "Fulfilled", StatsTone.Done)]
    // Other common names.
    [TestCase("Canceled", "Cancelled", StatsTone.Problem)]
    [TestCase("Refunded", "Refunded", StatsTone.Problem)]
    [TestCase("ReturnRequested", "Return requested", StatsTone.Problem)]
    [TestCase("OnHold", "On hold", StatsTone.Caution)]
    [TestCase("AwaitingShipment", "Awaiting shipment", StatsTone.Caution)]
    [TestCase("Unpaid", "Unpaid", StatsTone.Caution)]
    [TestCase("Completed", "Completed", StatsTone.Done)]
    [TestCase("Shipped", "Shipped", StatsTone.Done)]
    [TestCase("Paid", "Paid", StatsTone.Done)]
    [TestCase("InFulfillment", "In fulfillment", StatsTone.Done)]
    public void Get_GuessesFromNames(string codeName, string displayName, StatsTone expected) =>
        Assert.That(OrderStatusTones.Get(codeName, displayName), Is.EqualTo(expected));

    [Test]
    public void Get_IgnoresCase() =>
        Assert.That(OrderStatusTones.Get("PAYMENT_FAILED", null), Is.EqualTo(StatsTone.Problem));

    [Test]
    public void Get_MatchesDisplayName_WhenCodeNameHasNoKeyword() =>
        Assert.That(OrderStatusTones.Get("Status3", "Delivered"), Is.EqualTo(StatsTone.Done));

    [Test]
    public void Get_ProblemBeforeCaution_BeforeDone()
    {
        Assert.That(OrderStatusTones.Get("PaidButFailed", null), Is.EqualTo(StatsTone.Problem));
        Assert.That(OrderStatusTones.Get("ShippingOnHold", null), Is.EqualTo(StatsTone.Caution));
    }

    [Test]
    public void Get_NoNames_Neutral() =>
        Assert.That(OrderStatusTones.Get(null, null), Is.EqualTo(StatsTone.Neutral));
}
