using Not.Application.Authentication.User;
using Not.Domain;

namespace NTS.Tests.Unit.Domain;

public class EntityEqualityTests
{
    // Different Guids with the same hash code: GetHashCode folds 128 bits into 32 (ADR-0009).
    static readonly Guid FIRST = Guid.Parse("ef1270d1-8aca-4670-bc3b-1bbbbddf83fc");
    static readonly Guid SECOND = Guid.Parse("efed702e-8a35-468f-bc3b-1bbbbddf83fc");

    [Fact]
    public void The_pinned_ids_are_different_and_collide_on_the_hash_code()
    {
        Assert.NotEqual(FIRST, SECOND);
        Assert.Equal(FIRST.GetHashCode(), SECOND.GetHashCode());
    }

    [Fact]
    public void Entities_of_the_same_type_with_the_same_id_are_equal()
    {
        var rider = new Rider(FIRST);
        var sameRider = new Rider(FIRST);

        Assert.True(rider.Equals(sameRider));
        Assert.True(rider.Equals((object)sameRider));
        Assert.True(rider == sameRider);
        Assert.False(rider != sameRider);
        Assert.Equal(rider.GetHashCode(), sameRider.GetHashCode());
    }

    [Fact]
    public void The_hash_code_of_an_entity_is_the_hash_code_of_its_id()
    {
        Assert.Equal(FIRST.GetHashCode(), new Rider(FIRST).GetHashCode());
    }

    [Fact]
    public void The_same_id_on_another_type_is_not_equal()
    {
        var rider = new Rider(FIRST);
        var mount = new Mount(FIRST);

        Assert.False(rider.Equals(mount));
        Assert.False(rider.Equals((object)mount));
        Assert.True(rider != mount);
    }

    [Fact]
    public void A_derived_type_is_not_equal_to_its_base_with_the_same_id()
    {
        Assert.False(new Rider(FIRST).Equals(new ChampionRider(FIRST)));
        Assert.False(new ChampionRider(FIRST).Equals(new Rider(FIRST)));
    }

    [Fact]
    public void Different_ids_that_share_a_hash_code_are_not_equal()
    {
        var first = new Rider(FIRST);
        var second = new Rider(SECOND);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.False(first.Equals(second));
        Assert.False(first.Equals((object)second));
        Assert.True(first != second);
    }

    [Fact]
    public void Different_ids_that_share_a_hash_code_stay_apart_in_a_HashSet()
    {
        var first = new Rider(FIRST);
        var second = new Rider(SECOND);

        var set = new HashSet<Entity> { first, second };

        Assert.Equal(2, set.Count);
        Assert.Contains(first, set);
        Assert.Contains(second, set);
        Assert.True(set.Remove(first));
        Assert.DoesNotContain(first, set);
        Assert.Contains(second, set);
    }

    [Fact]
    public void An_entity_is_never_equal_to_null_or_to_something_that_is_not_an_entity()
    {
        var rider = new Rider(FIRST);

        Assert.False(rider.Equals(null));
        Assert.False(rider.Equals((object?)null));
        Assert.False(rider.Equals(FIRST));
        Assert.False(rider == null);
    }

    [Fact]
    public void An_entity_without_an_id_gets_its_own_Guid()
    {
        var first = new Rider();
        var second = new Rider();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void User_models_follow_the_same_rules()
    {
        var user = new NUserModel("ana@example.test", id: FIRST);

        Assert.True(user.Equals(new NUserModel("other@example.test", id: FIRST)));
        Assert.False(user.Equals(new NUserModel("ana@example.test", id: SECOND)));
        Assert.Equal(user.GetHashCode(), new NUserModel("ana@example.test", id: SECOND).GetHashCode());
        Assert.False(user.Equals(null));
        Assert.Equal(2, new HashSet<NUserModel> { user, new("ana@example.test", id: SECOND) }.Count);
        Assert.NotEqual(Guid.Empty, new NUserModel("ana@example.test").Id);
    }

    class Rider : Entity
    {
        public Rider(Guid? id = null)
            : base(id) { }

        public override string ToString()
        {
            return nameof(Rider);
        }
    }

    sealed class Mount : Entity
    {
        public Mount(Guid? id = null)
            : base(id) { }

        public override string ToString()
        {
            return nameof(Mount);
        }
    }

    sealed class ChampionRider : Rider
    {
        public ChampionRider(Guid? id = null)
            : base(id) { }

        public override string ToString()
        {
            return nameof(ChampionRider);
        }
    }
}
