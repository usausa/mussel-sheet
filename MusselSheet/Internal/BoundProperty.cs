namespace MusselSheet.Internal;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

internal sealed class BoundProperty
{
    private readonly PropertyInfo info;

    private readonly object? emptyValue;

    private BoundProperty(PropertyInfo property, object? emptyValue)
    {
        info = property;
        var setter = property.SetMethod;
        CanWrite = setter is { IsPublic: true } && !Array.Exists(setter.ReturnParameter.GetRequiredCustomModifiers(), x => x == typeof(IsExternalInit));
        var underlying = Nullable.GetUnderlyingType(property.PropertyType);
        ValueType = underlying ?? property.PropertyType;
        this.emptyValue = emptyValue;
    }

    public string Name => info.Name;

    public Type ValueType { get; }

    public bool CanWrite { get; }

    public static BoundProperty Of<T, TValue>(Expression<Func<T, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var body = expression.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if ((body is MemberExpression { Member: PropertyInfo property } member) && (member.Expression == expression.Parameters[0]))
        {
            return new BoundProperty(property, default(TValue));
        }

        throw new ArgumentException("The expression must be a property of the parameter, such as x => x.Name.", nameof(expression));
    }

    public static bool IsWholeNumber(Type type) =>
        (type == typeof(int)) || (type == typeof(long)) || (type == typeof(short)) || (type == typeof(byte)) ||
        (type == typeof(uint)) || (type == typeof(ulong)) || (type == typeof(ushort)) || (type == typeof(sbyte));

    public static bool IsNumber(Type type) => IsWholeNumber(type) || (type == typeof(decimal)) || (type == typeof(double)) || (type == typeof(float));

    public object? Get(object target) => info.GetValue(target);

    public void Set(object target, object? value) => info.SetValue(target, value);

    public bool TryConvert(object? value, out object? result)
    {
        result = emptyValue;
        if (value is null)
        {
            return true;
        }

        if (ValueType.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }

        if (IsWholeNumber(ValueType) && HasFraction(value))
        {
            return false;
        }

        var converted = ValueType.IsEnum ? ToEnum(value) : CellText.ConvertTo(value, ValueType);
        if (converted is null)
        {
            return false;
        }

        result = converted;
        return true;
    }

    // Compared after converting to the property type, so a decimal 5 in the cell equals an int 5
    public bool IsSame(object? cell, object? value) =>
        Equals(cell, value) || ((cell is not null) && (value is not null) && TryConvert(cell, out var converted) && Equals(converted, value));

    private static bool HasFraction(object value) => value switch
    {
        decimal number => !decimal.IsInteger(number),
        double number => !double.IsInteger(number),
        float number => !float.IsInteger(number),
        _ => false
    };

    private object? ToEnum(object value) =>
        (value is string text) && Enum.TryParse(ValueType, text, true, out var parsed) ? parsed : null;
}
