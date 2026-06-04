using Kronxy.Domain.Abstractions;
using Kronxy.Domain.Users.Events;

namespace Kronxy.Domain.Users;

public sealed class User : Entity
{
    private User(
        Guid id,
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        Guid roleId,
        DateTime createdOnUtc)
        : base(id)
    {
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        RoleId = roleId;
        IsActive = true;
        CreatedOnUtc = createdOnUtc;
    }

    private User()
    {
    }

    public Username Username { get; private set; }

    public FirstName FirstName { get; private set; }

    public LastName LastName { get; private set; }

    public Email Email { get; private set; }

    public PhoneNumber? PhoneNumber { get; private set; }

    public Guid RoleId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedOnUtc { get; private set; }

    public DateTime? UpdatedOnUtc { get; private set; }

    public DateTime? DeletedOnUtc { get; private set; }

    public static User Create(
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        Guid roleId,
        DateTime createdOnUtc)
    {
        var user = new User(
            Guid.NewGuid(),
            username,
            firstName,
            lastName,
            email,
            phoneNumber,
            roleId,
            createdOnUtc);

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

        return user;
    }

    public void Update(
        Username username,
        FirstName firstName,
        LastName lastName,
        Email email,
        PhoneNumber? phoneNumber,
        Guid roleId,
        DateTime updatedOnUtc)
    {
        Username = username;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        RoleId = roleId;
        UpdatedOnUtc = updatedOnUtc;
    }

    public void Deactivate(DateTime deletedOnUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeletedOnUtc = deletedOnUtc;
        UpdatedOnUtc = deletedOnUtc;
    }

    public void Activate(DateTime utcNow)
    {
        IsActive = true;
        UpdatedOnUtc = utcNow;
        DeletedOnUtc = null;
    }
}