using Microsoft.AspNetCore.Identity;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AppUser = SaksAppWeb.Models.ApplicationUser;

namespace SaksAppWeb.Tests;

public class TestUserManager : UserManager<AppUser>
{
    private readonly List<AppUser> _users;

    public TestUserManager() : base(
        new Mock<IUserStore<AppUser>>().Object,
        null!, null!, null!, null!, null!, null!, null!, null!)
    {
        _users = new List<AppUser>();
    }

    public void AddUser(AppUser user) => _users.Add(user);

    public override IQueryable<AppUser> Users => new AsyncUserQueryable<AppUser>(_users);
}

public class AsyncUserQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>, IOrderedQueryable<T>
{
    private readonly List<T> _data;

    public AsyncUserQueryable(List<T> data)
    {
        _data = data;
    }

    public Type ElementType => typeof(T);
    public Expression Expression => Expression.Constant(this);
    public IQueryProvider Provider => new AsyncUserQueryProvider<T>(_data);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new AsyncUserEnumerator<T>(_data);
    }

    public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _data.GetEnumerator();
}

public class AsyncUserQueryProvider<T> : IQueryProvider
{
    private readonly List<T> _data;

    public AsyncUserQueryProvider(List<T> data)
    {
        _data = data;
    }

    public IQueryable CreateQuery(Expression expression) => new AsyncUserQueryable<T>(_data);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new AsyncUserQueryable<TElement>(_data.Cast<TElement>().ToList());
    public object? Execute(Expression expression) => Execute<object>(expression)!;

    public TResult Execute<TResult>(Expression expression)
    {
        var resultType = typeof(TResult);
        if (resultType.IsGenericType)
        {
            var genericDef = resultType.GetGenericTypeDefinition();
            if (genericDef == typeof(IAsyncEnumerable<>))
            {
                var elementType = resultType.GetGenericArguments()[0];
                if (elementType == typeof(T))
                {
                    return (TResult)(object)new AsyncUserQueryable<T>(_data);
                }
            }
        }
        return _data.AsQueryable().Provider.Execute<TResult>(expression);
    }
}

public class AsyncUserEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly List<T> _data;
    private int _index = -1;

    public AsyncUserEnumerator(List<T> data)
    {
        _data = data;
    }

    public T Current => _index >= 0 && _index < _data.Count ? _data[_index] : default!;

    public ValueTask<bool> MoveNextAsync()
    {
        _index++;
        return ValueTask.FromResult(_index < _data.Count);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}