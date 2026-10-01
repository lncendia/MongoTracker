using System.Linq.Expressions;
using System.Reflection;

namespace Incendia.MongoTracker.Metadata;

/// <summary>
/// Provides fast access to a property through compiled delegates instead of reflection calls.
/// </summary>
internal sealed class PropertyAccessor
{
  #region Fields

  /// <summary>
  /// Compiled getter of the property.
  /// </summary>
  private readonly Func<object, object?> _getter;

  /// <summary>
  /// Compiled setter of the property.
  /// </summary>
  private readonly Action<object, object?> _setter;

  #endregion

  #region Properties

  /// <summary>
  /// The name of the property.
  /// </summary>
  public string Name { get; }

  /// <summary>
  /// The first generic argument of the property type (the element type of generic collections), if any.
  /// </summary>
  public Type? ElementType { get; }

  #endregion

  #region Methods

  /// <summary>
  /// Reads the value of the property.
  /// </summary>
  /// <param name="target">The object to read the value from.</param>
  /// <returns>The value of the property.</returns>
  public object? GetValue(object target) => _getter(target);

  /// <summary>
  /// Writes the value of the property.
  /// </summary>
  /// <param name="target">The object to write the value to.</param>
  /// <param name="value">The value to write.</param>
  public void SetValue(object target, object? value) => _setter(target, value);

  #endregion

  #region Constructors

  /// <summary>
  /// Creates an accessor for a readable and writable instance property.
  /// </summary>
  /// <param name="property">The property to access.</param>
  public PropertyAccessor(PropertyInfo property)
  {
    Name = property.Name;
    Type[] genericArguments = property.PropertyType.GetGenericArguments();
    ElementType = genericArguments.Length > 0 ? genericArguments[0] : null;

    // Properties of structs cannot be written through a compiled cast (it would modify a copy), so use reflection
    if (property.DeclaringType!.IsValueType)
    {
      _getter = property.GetValue;
      _setter = property.SetValue;
      return;
    }

    ParameterExpression target = Expression.Parameter(typeof(object), "target");
    ParameterExpression value = Expression.Parameter(typeof(object), "value");
    MemberExpression member = Expression.Property(Expression.Convert(target, property.DeclaringType), property);

    // target => (object)((TDeclaring)target).Property
    _getter = Expression
      .Lambda<Func<object, object?>>(Expression.Convert(member, typeof(object)), target)
      .Compile();

    // (target, value) => ((TDeclaring)target).Property = (TProperty)value
    _setter = Expression
      .Lambda<Action<object, object?>>(
        Expression.Assign(member, Expression.Convert(value, property.PropertyType)), target, value)
      .Compile();
  }

  #endregion
}
