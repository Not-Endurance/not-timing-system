using System.Net;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using NoTiming.Api.Features.Reference;

namespace NTS.Tests.Integration;

/// <summary>
/// Whose the failure is when a read fails while it runs the filter of a caller (the rest-api skill): the expression's,
/// which the caller can correct and is answered 400, or the database's, which is an error of the server. The exception tells.
/// The database is not needed to tell, only what each failure is.
/// </summary>
public sealed class ExpressionRefusalTests
{
    [Theory]
    [InlineData(2, "BadValue")] // a division by zero: MongoDB 8 answers so on a row, and 6.0 when it folds a constant
    [InlineData(9, "FailedToParse")]
    [InlineData(14, "TypeMismatch")]
    [InlineData(15, "Overflow")]
    [InlineData(241, "ConversionFailure")]
    [InlineData(34454, "Location34454")] // a part of a text that starts past its end
    [InlineData(16608, "Location16608")] // a division by zero that MongoDB 6.0 meets on a row
    [InlineData(16610, "Location16610")] // a modulo by zero
    [InlineData(51091, "Location51091")] // a pattern that is not a regular expression
    public void A_command_that_the_database_refuses_for_the_query_is_the_expressions_failure(int code, string name)
    {
        Assert.True(ReferencePages.IsOfTheExpression(Refused(code, name)));
    }

    [Theory]
    [InlineData(6, "HostUnreachable")]
    [InlineData(13, "Unauthorized")]
    [InlineData(18, "AuthenticationFailed")]
    [InlineData(43, "CursorNotFound")]
    [InlineData(50, "MaxTimeMSExpired")]
    [InlineData(89, "NetworkTimeout")]
    [InlineData(91, "ShutdownInProgress")]
    [InlineData(189, "PrimarySteppedDown")]
    [InlineData(262, "ExceededTimeLimit")]
    [InlineData(10107, "NotWritablePrimary")]
    [InlineData(11600, "InterruptedAtShutdown")]
    [InlineData(13436, "NotPrimaryOrSecondary")]
    public void A_command_that_the_database_refuses_for_its_own_state_is_the_databases_failure(int code, string name)
    {
        Assert.False(ReferencePages.IsOfTheExpression(Refused(code, name)));
    }

    [Fact]
    public void A_command_with_no_name_for_its_code_is_the_databases_failure()
    {
        Assert.False(ReferencePages.IsOfTheExpression(Refused(11, null)));
    }

    [Fact]
    public void An_error_that_the_driver_meets_in_a_part_of_the_expression_that_has_no_row_is_the_expressions_failure()
    {
        Assert.True(ReferencePages.IsOfTheExpression(new TargetInvocationException(new DivideByZeroException())));
    }

    [Fact]
    public void A_connection_that_is_lost_or_a_wait_that_ends_is_the_databases_failure()
    {
        var server = new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017));

        Assert.False(ReferencePages.IsOfTheExpression(new MongoConnectionException(new ConnectionId(server), "lost")));
        Assert.False(ReferencePages.IsOfTheExpression(new TimeoutException("no server was selected")));
        Assert.False(
            ReferencePages.IsOfTheExpression(new MongoAuthenticationException(new ConnectionId(server), "refused"))
        );
    }

    [Fact]
    public void A_document_that_cannot_be_read_and_any_other_error_are_the_databases_failure()
    {
        Assert.False(ReferencePages.IsOfTheExpression(new FormatException("a document that is not a row")));
        Assert.False(ReferencePages.IsOfTheExpression(new InvalidOperationException("something else")));
        Assert.False(ReferencePages.IsOfTheExpression(new OperationCanceledException()));
    }

    static MongoCommandException Refused(int code, string? name)
    {
        var server = new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017));
        var result = new BsonDocument
        {
            { "ok", 0 },
            { "errmsg", "refused" },
            { "code", code },
        };
        if (name != null)
        {
            result["codeName"] = name;
        }

        return new MongoCommandException(new ConnectionId(server), "refused", new BsonDocument(), result);
    }
}
