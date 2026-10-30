using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Developer;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0012 and #643: the Developer seeds a Tenant Root and grants Developer by command, never through a route. A Tenant
/// becomes operational, able to hold Events, when it has a Tenant Root. The commands run here over users and Tenants in
/// the shape the Api keeps them, on a MongoDB in a container. Each test makes the accounts and the Tenants of its own,
/// because the database is shared by the tests of the class.
/// </summary>
public sealed class DeveloperCommandTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public DeveloperCommandTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_dry_run_says_what_it_would_do_and_changes_nothing()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        var before = await StoredAsync(account.Email);

        var result = await SeedAsync(tenant, account.Email, apply: false);

        Assert.Equal(DeveloperOutcome.WouldChange, result.Outcome);
        Assert.Equal(before, await StoredAsync(account.Email));
        Assert.Equal(0, await TenantRootsAsync(tenant));
    }

    [Fact]
    public async Task Applying_makes_the_account_a_Tenant_Root_of_the_Tenant_which_makes_the_Tenant_operational()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);

        var result = await SeedAsync(tenant, account.Email, apply: true);

        Assert.Equal(DeveloperOutcome.Changed, result.Outcome);
        Assert.Equal(1, result.TenantRoots);
        var membership = MembershipOf(await StoredAsync(account.Email), tenant)!;
        Assert.Equal(["tenant-root"], membership["Roles"].AsBsonArray.Select(x => x.AsString));
        Assert.Equal(1, await TenantRootsAsync(tenant));
    }

    [Fact]
    public async Task Applying_again_changes_nothing_and_says_so()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        await SeedAsync(tenant, account.Email, apply: true);
        var once = await StoredAsync(account.Email);

        var again = await SeedAsync(tenant, account.Email, apply: true);

        Assert.Equal(DeveloperOutcome.AlreadyDone, again.Outcome);
        Assert.Equal(1, again.TenantRoots);
        Assert.Equal(once, await StoredAsync(account.Email));
    }

    [Fact]
    public async Task The_role_goes_into_the_Membership_of_that_Tenant_and_everything_else_of_the_account_stays_as_it_was()
    {
        var tenant = await NewTenantAsync();
        var other = await NewTenantAsync();
        var account = await NewAccountAsync(tenant, otherMembership: other);
        var before = await StoredAsync(account.Email);

        await SeedAsync(tenant, account.Email, apply: true);

        var after = await StoredAsync(account.Email);
        var memberships = after["Memberships"].AsBsonArray;
        Assert.Equal(2, memberships.Count);
        Assert.Empty(MembershipOf(after, other)!["Roles"].AsBsonArray);
        Assert.Equal(before["HomeTenantId"], after["HomeTenantId"]);
        Assert.Equal(before["Roles"], after["Roles"]);
        Assert.Equal(before["Name"], after["Name"]);
        Assert.Equal(before["SecurityStamp"], after["SecurityStamp"]);
        Assert.Equal(before["Passkeys"], after["Passkeys"]);
        after.Remove("Memberships");
        before.Remove("Memberships");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task An_account_that_is_not_a_member_of_the_Tenant_yet_becomes_one_with_the_role()
    {
        var tenant = await NewTenantAsync();
        var home = await NewTenantAsync();
        var account = await NewAccountAsync(home);

        var result = await SeedAsync(tenant, account.Email, apply: true);

        Assert.Equal(DeveloperOutcome.Changed, result.Outcome);
        var after = await StoredAsync(account.Email);
        Assert.Equal(["tenant-root"], MembershipOf(after, tenant)!["Roles"].AsBsonArray.Select(x => x.AsString));
        Assert.Empty(MembershipOf(after, home)!["Roles"].AsBsonArray);
        Assert.Equal(home, after["HomeTenantId"].AsString);
    }

    [Fact]
    public async Task An_account_from_before_Tenants_that_has_no_Memberships_gets_the_first()
    {
        var tenant = await NewTenantAsync();
        var email = UserSeed.NewEmail("legacy");
        await UserSeed.AddLegacyUserAsync(_mongo.ConnectionString, email);

        var result = await SeedAsync(tenant, email, apply: true);

        Assert.Equal(DeveloperOutcome.Changed, result.Outcome);
        var after = await StoredAsync(email);
        Assert.Single(after["Memberships"].AsBsonArray);
        Assert.False(after.Contains("HomeTenantId")); // the home Tenant is the person's own and is set only by placing them
    }

    [Fact]
    public async Task A_Membership_that_has_no_roles_member_at_all_gets_the_role_like_one_that_has_an_empty_list()
    {
        var tenant = await NewTenantAsync();
        var email = UserSeed.NewEmail("handmade");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document => document["Memberships"] = new BsonArray { new BsonDocument("TenantId", tenant) }
        );

        var result = await SeedAsync(tenant, email, apply: true);

        Assert.Equal(DeveloperOutcome.Changed, result.Outcome);
        var membership = MembershipOf(await StoredAsync(email), tenant)!;
        Assert.Equal(["tenant-root"], membership["Roles"].AsBsonArray.Select(x => x.AsString));
        Assert.Equal(1, await TenantRootsAsync(tenant));
    }

    [Fact]
    public async Task More_than_one_account_may_be_a_Tenant_Root_of_the_same_Tenant()
    {
        var tenant = await NewTenantAsync();
        var first = await NewAccountAsync(tenant);
        var second = await NewAccountAsync(tenant);

        await SeedAsync(tenant, first.Email, apply: true);
        var result = await SeedAsync(tenant, second.Email, apply: true);

        Assert.Equal(2, result.TenantRoots);
        Assert.Equal(2, await TenantRootsAsync(tenant));
    }

    [Fact]
    public async Task An_account_that_does_not_exist_is_refused_and_nothing_is_written()
    {
        var tenant = await NewTenantAsync();

        var result = await SeedAsync(tenant, UserSeed.NewEmail("nobody"), apply: true);

        Assert.Equal(DeveloperOutcome.AccountNotFound, result.Outcome);
        Assert.Equal(0, await TenantRootsAsync(tenant));
    }

    [Fact]
    public async Task A_Tenant_that_does_not_exist_is_refused_and_nothing_is_written()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        var before = await StoredAsync(account.Email);

        var result = await SeedAsync("country-nowhere", account.Email, apply: true);

        Assert.Equal(DeveloperOutcome.TenantNotFound, result.Outcome);
        Assert.Equal(before, await StoredAsync(account.Email));
    }

    [Theory]
    [InlineData("  {0}  ")]
    [InlineData("{1}")]
    public async Task The_email_is_found_whatever_its_case_and_the_spaces_around_it(string form)
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        var typed = string.Format(form, account.Email, account.Email.ToUpperInvariant());

        var result = await SeedAsync(tenant, typed, apply: true);

        Assert.Equal(DeveloperOutcome.Changed, result.Outcome);
    }

    [Fact]
    public async Task Granting_Developer_sets_the_flag_and_nothing_else_and_granting_it_again_changes_nothing()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        var before = await StoredAsync(account.Email);

        var dry = await GrantAsync(account.Email, apply: false);
        var unchanged = await StoredAsync(account.Email);
        var granted = await GrantAsync(account.Email, apply: true);
        var once = await StoredAsync(account.Email);
        var again = await GrantAsync(account.Email, apply: true);

        Assert.Equal(DeveloperOutcome.WouldChange, dry.Outcome);
        Assert.Equal(before, unchanged);
        Assert.Equal(DeveloperOutcome.Changed, granted.Outcome);
        Assert.True(once["IsDeveloper"].AsBoolean);
        once.Remove("IsDeveloper");
        Assert.Equal(before, once);
        Assert.Equal(DeveloperOutcome.AlreadyDone, again.Outcome);
        Assert.True((await StoredAsync(account.Email))["IsDeveloper"].AsBoolean);
    }

    [Fact]
    public async Task Granting_Developer_to_an_account_that_does_not_exist_is_refused()
    {
        var result = await GrantAsync(UserSeed.NewEmail("nobody"), apply: true);

        Assert.Equal(DeveloperOutcome.AccountNotFound, result.Outcome);
    }

    [Fact]
    public async Task The_command_line_applies_only_when_told_to_and_needs_to_be_told_where_what_and_to_whom()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);
        var common = new[] { "--connection-string", _mongo.ConnectionString, "--email", account.Email };

        var dry = await RunAsync(DeveloperTool.SeedTenantRoot, [.. common, "--tenant", tenant]);
        var applied = await RunAsync(DeveloperTool.SeedTenantRoot, [.. common, "--tenant", tenant, "--apply"]);
        var noTenant = await RunAsync(DeveloperTool.SeedTenantRoot, common);
        var noEmail = await RunAsync(DeveloperTool.GrantDeveloper, ["--connection-string", _mongo.ConnectionString]);
        var unknown = await RunAsync(
            DeveloperTool.SeedTenantRoot,
            [.. common, "--tenant", "country-nowhere", "--apply"]
        );
        var granted = await RunAsync(DeveloperTool.GrantDeveloper, [.. common, "--apply"]);

        Assert.Equal(0, dry.Exit);
        Assert.Contains("Dry run", dry.Output);
        Assert.Equal(0, applied.Exit);
        Assert.Equal(1, await TenantRootsAsync(tenant));
        Assert.Equal(1, noTenant.Exit);
        Assert.Contains("--tenant is required", noTenant.Error);
        Assert.Equal(1, noEmail.Exit);
        Assert.Contains("--email is required", noEmail.Error);
        Assert.Equal(1, unknown.Exit);
        Assert.Equal(0, granted.Exit);
    }

    [Fact]
    public async Task What_a_command_prints_names_neither_the_connection_string_nor_a_secret()
    {
        var tenant = await NewTenantAsync();
        var account = await NewAccountAsync(tenant);

        var run = await RunAsync(
            DeveloperTool.SeedTenantRoot,
            ["--connection-string", _mongo.ConnectionString, "--email", account.Email, "--tenant", tenant, "--apply"]
        );

        Assert.DoesNotContain(_mongo.ConnectionString, run.Output + run.Error);
        Assert.DoesNotContain("SecurityStamp", run.Output + run.Error);
    }

    IMongoDatabase Database()
    {
        return new MongoClient(_mongo.ConnectionString).GetDatabase(UserSeed.DATABASE);
    }

    Task<DeveloperResult> SeedAsync(string tenant, string email, bool apply)
    {
        return DeveloperCommands.SeedTenantRoot(Database(), tenant, email, apply);
    }

    Task<DeveloperResult> GrantAsync(string email, bool apply)
    {
        return DeveloperCommands.GrantDeveloper(Database(), email, apply);
    }

    /// <summary>A Tenant as the Api keeps it, with an id of its own so that no test sees another's roles.</summary>
    async Task<string> NewTenantAsync()
    {
        var id = $"country-{Guid.NewGuid():N}"[..16];
        await Database()
            .GetCollection<BsonDocument>("tenants")
            .InsertOneAsync(
                new BsonDocument
                {
                    { "_id", id },
                    { "Name", "Tenant " + id },
                    { "Kind", "country" },
                }
            );
        return id;
    }

    /// <summary>An account as the Api keeps it: identity fields, a profile, a home Tenant and its Membership.</summary>
    async Task<Account> NewAccountAsync(string homeTenant, string? otherMembership = null)
    {
        var email = UserSeed.NewEmail("dev");
        await UserSeed.AddLegacyUserAsync(
            _mongo.ConnectionString,
            email,
            shape: document =>
            {
                document["HomeTenantId"] = homeTenant;
                var memberships = new BsonArray
                {
                    new BsonDocument { { "TenantId", homeTenant }, { "Roles", new BsonArray() } },
                };
                if (otherMembership != null)
                {
                    memberships.Add(new BsonDocument { { "TenantId", otherMembership }, { "Roles", new BsonArray() } });
                }

                document["Memberships"] = memberships;
                document["EmailConfirmed"] = true;
                document["SecurityStamp"] = "stamp-" + Guid.NewGuid();
                document["Passkeys"] = new BsonArray { UserSeed.StoredPasskey([1, 2, 3, 4, 5, 6, 7, 8]) };
            }
        );
        return new Account(email);
    }

    async Task<BsonDocument> StoredAsync(string email)
    {
        var users = UserSeed.Users(_mongo.ConnectionString);
        return await users.Find(new BsonDocument("Email", email.Trim().ToLowerInvariant())).FirstAsync();
    }

    async Task<long> TenantRootsAsync(string tenant)
    {
        var users = UserSeed.Users(_mongo.ConnectionString);
        return await users.CountDocumentsAsync(
            new BsonDocument(
                "Memberships",
                new BsonDocument("$elemMatch", new BsonDocument { { "TenantId", tenant }, { "Roles", "tenant-root" } })
            )
        );
    }

    static BsonDocument? MembershipOf(BsonDocument user, string tenant)
    {
        return user["Memberships"]
            .AsBsonArray.Select(x => x.AsBsonDocument)
            .FirstOrDefault(x => x["TenantId"].AsString == tenant);
    }

    static async Task<Run> RunAsync(Func<string[], TextWriter, TextWriter, Task<int>> command, string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await command(args, output, error);
        return new Run(exit, output.ToString(), error.ToString());
    }

    sealed class Account
    {
        public Account(string email)
        {
            Email = email;
        }

        public string Email { get; }
    }

    sealed class Run
    {
        public Run(int exit, string output, string error)
        {
            Exit = exit;
            Output = output;
            Error = error;
        }

        public int Exit { get; }
        public string Output { get; }
        public string Error { get; }
    }
}
