using Not.Domain;

namespace Not.Blazor.Forms;

// Probably remove these

/// <summary>
/// Abstraction enabling seemless validation per form field
/// </summary>
public interface IForm
{
    /// <summary>
    /// Adds validation error for a specific input field, or generic error if <paramref name="field"/> is null
    /// </summary>
    /// <param name="field">Name of the input field. The "field - name" mapping is a responsibility of the implementations</param>
    /// <param name="message">Validation message</param>
    /// <returns></returns>
    Task AddValidationError(string? field, string message);
}

/// <summary>
/// Abstraction representing a create form implementaiton
/// </summary>
/// <typeparam name="T">Type of Domain Entity to be created</typeparam>
public interface ICreateForm<T> : IForm
{
    /// <summary>
    /// Construct an instance of <typeparamref name="T"/> based on the form inputs that is suitable for create.
    /// I.e. without pre-existing Identifier
    /// </summary>
    /// <returns><typeparamref name="T"/> instance for a new Domain Entity</returns>
    T SubmitCreate();
}

/// <summary>
/// An abstraction representing an update form implementation
/// </summary>
/// <typeparam name="T">Type of Domain Entity to be updated</typeparam>
public interface IUpdateForm<T> : IForm
{
    /// <summary>
    /// Constructs an instance of update model from an instance of <typeparamref name="T"/> 
    /// used to display current entity state in the form
    /// </summary>
    void SetUpdateModel(T entity);
    /// <summary>
    /// Construct an instance of <typeparamref name="T"/> based on the form inputs that is suitable for update.
    /// I.e. with pre-existing Identifier
    /// </summary>
    /// <returns><typeparamref name="T"/> instance for existing Domain Entity to be updated</returns>
    T SubmitUpdate();
}
