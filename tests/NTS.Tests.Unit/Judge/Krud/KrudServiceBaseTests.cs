using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;
using Not.Krud.Services;

namespace NTS.Judge.Tests.Krud;

public class KrudServiceBaseTests
{
    [Fact]
    public async Task ReadMany_WhenRepositorySucceeds_ReturnsItems()
    {
        var first = new TestEntity("First", 101);
        var second = new TestEntity("Second", 102);
        var repository = new CascadingTestRepository { Items = [first, second] };
        var service = new TestService(repository, []);

        var result = await service.ReadMany();

        Assert.Equal(new[] { first, second }, result);
        Assert.Equal(1, repository.ReadManyCalls);
    }

    [Fact]
    public async Task ReadMany_WhenRepositoryThrows_PropagatesException()
    {
        var expected = new InvalidOperationException("read-many-failed");
        var repository = new CascadingTestRepository { ReadManyException = expected };
        var service = new TestService(repository, []);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadMany());

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Create_WhenModelIdIsNull_CreatesMappedEntity()
    {
        var repository = new CascadingTestRepository();
        var service = new TestService(repository, []);
        var model = new TestModel { Id = null, Name = "Created" };

        await service.Create(model);

        Assert.Equal(1, repository.CreateCalls);
        var created = Assert.Single(repository.Items);
        Assert.Equal("Created", created.Name);
        Assert.NotNull(model.Id);
    }

    [Fact]
    public async Task Create_WhenModelIdIsNotNull_ThrowsGuardException()
    {
        var repository = new CascadingTestRepository();
        var service = new TestService(repository, []);
        var model = new TestModel { Id = 7, Name = "Created" };

        await Assert.ThrowsAsync<GuardException>(() => service.Create(model));

        Assert.Equal(0, repository.CreateCalls);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_WhenMapperThrows_DoesNotCallRepository()
    {
        var repository = new CascadingTestRepository();
        var service = new TestService(repository, []);
        var model = new TestModel { Name = "Created", ThrowOnMapToEntity = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(model));

        Assert.Equal(0, repository.CreateCalls);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_WhenRepositoryThrows_PropagatesException()
    {
        var expected = new InvalidOperationException("create-failed");
        var repository = new CascadingTestRepository { CreateException = expected };
        var service = new TestService(repository, []);
        var model = new TestModel { Name = "Created" };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Create(model));

        Assert.Same(expected, actual);
        Assert.Equal(1, repository.CreateCalls);
    }

    [Fact]
    public async Task Update_WhenModelIdIsSet_UpdatesEntityAndReflectsMirrors()
    {
        var repository = new CascadingTestRepository();
        var mirrorA = new TestMirror();
        var mirrorB = new TestMirror();
        var service = new TestService(repository, [mirrorA, mirrorB]);
        var model = new TestModel { Id = 9, Name = "Updated" };

        await service.Update(model);

        Assert.Equal(1, repository.UpdateCalls);
        var updated = Assert.Single(repository.Items);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(model.Id, updated.Id);
        Assert.Equal(updated, Assert.Single(mirrorA.Reflected));
        Assert.Equal(updated, Assert.Single(mirrorB.Reflected));
    }

    [Fact]
    public async Task Update_WhenModelIdIsNull_ThrowsGuardException()
    {
        var repository = new CascadingTestRepository();
        var mirror = new TestMirror();
        var service = new TestService(repository, [mirror]);
        var model = new TestModel { Id = null, Name = "Updated" };

        await Assert.ThrowsAsync<GuardException>(() => service.Update(model));

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Empty(mirror.Reflected);
    }

    [Fact]
    public async Task Update_WhenMapperThrows_DoesNotCallRepositoryOrMirrors()
    {
        var repository = new CascadingTestRepository();
        var mirror = new TestMirror();
        var service = new TestService(repository, [mirror]);
        var model = new TestModel
        {
            Id = 9,
            Name = "Updated",
            ThrowOnMapToEntity = true,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Update(model));

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Empty(mirror.Reflected);
    }

    [Fact]
    public async Task Update_WhenRepositoryThrows_DoesNotReflectMirrors()
    {
        var expected = new InvalidOperationException("update-failed");
        var repository = new CascadingTestRepository { UpdateException = expected };
        var mirror = new TestMirror();
        var service = new TestService(repository, [mirror]);
        var model = new TestModel { Id = 9, Name = "Updated" };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Update(model));

        Assert.Same(expected, actual);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Empty(mirror.Reflected);
    }

    [Fact]
    public async Task Update_WhenMirrorThrows_PropagatesAfterRepositoryUpdate()
    {
        var expected = new InvalidOperationException("mirror-failed");
        var repository = new CascadingTestRepository();
        var throwingMirror = new TestMirror(expected);
        var untouchedMirror = new TestMirror();
        var service = new TestService(repository, [throwingMirror, untouchedMirror]);
        var model = new TestModel { Id = 11, Name = "Updated" };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Update(model));

        Assert.Same(expected, actual);
        Assert.Equal(1, repository.UpdateCalls);
        Assert.Single(throwingMirror.Reflected);
        Assert.Empty(untouchedMirror.Reflected);
    }

    [Fact]
    public async Task PreviewDelete_WhenRepositoryDoesNotSupportCascade_ReturnsNoUsages()
    {
        var repository = new NonCascadingRepository();
        var service = new TestService(repository, []);
        var entity = new TestEntity("Delete", 77);

        var impact = await service.PreviewDelete(entity);

        Assert.False(impact.HasUsages);
        Assert.Equal("Delete", impact.Target);
    }

    [Fact]
    public async Task PreviewDelete_WhenRepositorySupportsCascade_ReturnsRepositoryImpact()
    {
        var expected = new KrudDeleteImpact("Delete", [new KrudDeleteUsage("Entity.Ref", "Owner", "Dependent")]);
        var repository = new CascadingTestRepository { PreviewImpact = expected };
        var service = new TestService(repository, []);
        var entity = new TestEntity("Delete", 77);

        var impact = await service.PreviewDelete(entity);

        Assert.Equal(expected, impact);
        Assert.Equal(1, repository.PreviewCalls);
    }

    [Fact]
    public async Task Delete_WhenRepositorySucceeds_DeletesEntity()
    {
        var entity = new TestEntity("Delete", 77);
        var repository = new CascadingTestRepository { Items = [entity] };
        var service = new TestService(repository, []);

        await service.Delete(entity);

        Assert.Equal(1, repository.DeleteCalls);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Delete_WhenPreviewContainsUsages_ThrowsDomainException()
    {
        var entity = new TestEntity("Delete", 77);
        var repository = new CascadingTestRepository
        {
            PreviewImpact = new KrudDeleteImpact("Delete", [new KrudDeleteUsage("Entity.Ref", "Owner", "Dependent")]),
        };
        var service = new TestService(repository, []);

        await Assert.ThrowsAsync<DomainException>(() => service.Delete(entity));

        Assert.Equal(0, repository.DeleteCalls);
    }

    [Fact]
    public async Task Delete_WhenRepositoryThrows_PropagatesException()
    {
        var expected = new InvalidOperationException("delete-failed");
        var entity = new TestEntity("Delete", 77);
        var repository = new CascadingTestRepository { DeleteException = expected };
        var service = new TestService(repository, []);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Delete(entity));

        Assert.Same(expected, actual);
        Assert.Equal(1, repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteCascade_WhenRepositorySupportsCascade_UsesCascadeDelete()
    {
        var entity = new TestEntity("Delete", 77);
        var repository = new CascadingTestRepository();
        var service = new TestService(repository, []);

        await service.DeleteCascade(entity);

        Assert.Equal(1, repository.CascadeDeleteCalls);
        Assert.Equal(0, repository.DeleteCalls);
    }

    [Fact]
    public async Task DeleteCascade_WhenRepositoryDoesNotSupportCascade_UsesRegularDelete()
    {
        var entity = new TestEntity("Delete", 77);
        var repository = new NonCascadingRepository { Items = [entity] };
        var service = new TestService(repository, []);

        await service.DeleteCascade(entity);

        Assert.Equal(1, repository.DeleteCalls);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task DeleteCascade_WhenCascadeRepositoryThrows_PropagatesException()
    {
        var expected = new InvalidOperationException("cascade-delete-failed");
        var entity = new TestEntity("Delete", 77);
        var repository = new CascadingTestRepository { CascadeDeleteException = expected };
        var service = new TestService(repository, []);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteCascade(entity));

        Assert.Same(expected, actual);
    }

    sealed class TestService : KrudServiceBase<TestEntity, TestModel>
    {
        public TestService(IRepository<TestEntity> repository, IEnumerable<IKrudMirrorService<TestEntity>> reflections)
            : base(repository, reflections) { }
    }

    sealed class TestMirror : IKrudMirrorService<TestEntity>
    {
        readonly Exception? _exception;

        public TestMirror(Exception? exception = null)
        {
            _exception = exception;
        }

        public List<TestEntity> Reflected { get; } = [];

        public Task Reflect(TestEntity entity)
        {
            Reflected.Add(entity);
            if (_exception != null)
            {
                throw _exception;
            }
            return Task.CompletedTask;
        }
    }

    sealed class TestModel : IKrudModel<TestEntity>, IKrudFormModel
    {
        public int? Id { get; set; }
        public string Name { get; set; } = "Unnamed";
        public bool ThrowOnMapToEntity { get; set; }

        public void MapFrom(TestEntity entity)
        {
            Id = entity.Id;
            Name = entity.Name;
        }

        public TestEntity MapToEntity()
        {
            if (ThrowOnMapToEntity)
            {
                throw new InvalidOperationException("map-to-entity-failed");
            }
            var entity = new TestEntity(Name, Id);
            Id = entity.Id;
            return entity;
        }
    }

    sealed class TestEntity : Entity
    {
        public TestEntity(string name, int? id = null)
            : base(id)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    class NonCascadingRepository : IRepository<TestEntity>
    {
        public List<TestEntity> Items { get; set; } = [];

        public int CreateCalls { get; protected set; }
        public int ReadManyCalls { get; protected set; }
        public int UpdateCalls { get; protected set; }
        public int DeleteCalls { get; protected set; }

        public Exception? ReadManyException { get; set; }
        public Exception? CreateException { get; set; }
        public Exception? UpdateException { get; set; }
        public Exception? DeleteException { get; set; }

        public virtual Task Create(TestEntity item)
        {
            CreateCalls++;
            if (CreateException != null)
            {
                throw CreateException;
            }
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<TestEntity?> Read(Expression<Func<TestEntity, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult(Items.FirstOrDefault(predicate));
        }

        public Task<TestEntity?> Read(int id)
        {
            return Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        }

        public virtual Task<IEnumerable<TestEntity>> ReadMany()
        {
            ReadManyCalls++;
            if (ReadManyException != null)
            {
                throw ReadManyException;
            }
            return Task.FromResult<IEnumerable<TestEntity>>(Items.ToList());
        }

        public Task<IEnumerable<TestEntity>> ReadMany(Expression<Func<TestEntity, bool>> filter)
        {
            var predicate = filter.Compile();
            return Task.FromResult<IEnumerable<TestEntity>>(Items.Where(predicate).ToList());
        }

        public virtual Task Update(TestEntity item)
        {
            UpdateCalls++;
            if (UpdateException != null)
            {
                throw UpdateException;
            }
            var index = Items.FindIndex(x => x.Id == item.Id);
            if (index >= 0)
            {
                Items[index] = item;
            }
            else
            {
                Items.Add(item);
            }
            return Task.CompletedTask;
        }

        public virtual Task Delete(TestEntity item)
        {
            DeleteCalls++;
            if (DeleteException != null)
            {
                throw DeleteException;
            }
            Items.Remove(item);
            return Task.CompletedTask;
        }

        public Task Delete(int id)
        {
            Items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteMany(Expression<Func<TestEntity, bool>> filter)
        {
            var predicate = filter.Compile();
            Items.RemoveAll(x => predicate(x));
            return Task.CompletedTask;
        }

        public Task DeleteMany(IEnumerable<TestEntity> items)
        {
            var set = items.ToHashSet();
            Items.RemoveAll(x => set.Contains(x));
            return Task.CompletedTask;
        }
    }

    sealed class CascadingTestRepository : NonCascadingRepository, IKrudCascadeRepository<TestEntity>
    {
        public int PreviewCalls { get; private set; }
        public int CascadeDeleteCalls { get; private set; }
        public KrudDeleteImpact PreviewImpact { get; set; } = new("Default", []);
        public Exception? CascadeDeleteException { get; set; }

        public Task<KrudDeleteImpact> PreviewDelete(TestEntity entity)
        {
            PreviewCalls++;
            return Task.FromResult(new KrudDeleteImpact(entity.ToString(), PreviewImpact.Usages));
        }

        public Task DeleteCascade(TestEntity entity)
        {
            CascadeDeleteCalls++;
            if (CascadeDeleteException != null)
            {
                throw CascadeDeleteException;
            }
            Items.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
