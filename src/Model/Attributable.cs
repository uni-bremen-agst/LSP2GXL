using LSP2GXL.Utils;

namespace LSP2GXL.Model;

public class UnknownAttribute(string name) : Exception($"Unknown attribute: {name}");

public abstract class Attributable
{
    /// <summary>
    /// The names of all numeric attributes (int and float) of any <see cref="Attributable"/>.
    /// Note that this set is shared among all instances of <see cref="Attributable"/>.
    /// Note also that it may contain names of attributes that are no longer present in any
    /// <see cref="Attributable"/> instance. This can happen if an attribute is removed from
    /// all instances. In this case, the name will remain in this set.
    /// </summary>
    private static readonly ThreadSafeHashSet<string> NumericAttributeNames = new();

    //----------------------------------
    // Toggle attributes
    //----------------------------------

    /// <summary>
    /// The set of toggle attributes. A toggle is set if it is contained in this
    /// list, otherwise it is unset. Conceptually, toggleAttributes is a HashSet,
    /// but HashSets are not serialized by Unity. That is why we use List instead.
    /// </summary>
    private readonly HashSet<string> toggleAttributes = [];

    public ISet<string> ToggleAttributes => toggleAttributes;

    /// <summary>
    /// If <paramref name="value"/> is true, the toggle with <paramref name="attributeName"/>
    /// will be set, otherwise it will be removed.
    /// </summary>
    /// <param name="attributeName">name of toggle attribute</param>
    /// <param name="value">value to be set</param>
    /// <remarks>All listeners will be notified in case of a change.</remarks>
    public void SetToggle(string attributeName, bool value)
    {
        if (value)
        {
            SetToggle(attributeName);
        }
        else
        {
            UnsetToggle(attributeName);
        }
    }

    /// <summary>
    /// Removes the toggle attribute with <paramref name="attributeName"/> if not
    /// already set. All listeners will be notified of this change.
    /// If the attribute is set already, nothing happens.
    /// </summary>
    /// <param name="attributeName">name of toggle attribute</param>
    /// <remarks>All listeners will be notified in case of a change.</remarks>
    public void SetToggle(string attributeName)
    {
        toggleAttributes.Add(attributeName);
    }

    /// <summary>
    /// Removes the toggle attribute with <paramref name="attributeName"/> if set.
    /// All listeners will be notified of this change.
    /// If no such attribute exists, nothing happens.
    /// </summary>
    /// <param name="attributeName">name of toggle attribute</param>
    /// <remarks>All listeners will be notified in case of a change.</remarks>
    public void UnsetToggle(string attributeName)
    {
        toggleAttributes.Remove(attributeName);
    }

    /// <summary>
    /// True if the toggle attribute with <paramref name="attributeName"/> is set.
    /// </summary>
    /// <param name="attributeName">name of toggle attribute</param>
    /// <returns>true if set</returns>
    public bool HasToggle(string attributeName)
    {
        return toggleAttributes.Contains(attributeName);
    }

    //----------------------------------
    // String attributes
    //----------------------------------

    public Dictionary<string, string> StringAttributes { get; } = new();

    /// <summary>
    /// Sets the string attribute with given <paramref name="attributeName"/> to <paramref name="value"/>
    /// if <paramref name="value"/> is different from <c>null</c>. If <paramref name="value"/> is <c>null</c>,
    /// the attribute will be removed.
    /// </summary>
    /// <param name="attributeName">name of the attribute</param>
    /// <param name="value">new value of the attribute</param>
    /// <remarks>This method will notify all listeners of this attributable</remarks>
    public void SetString(string attributeName, string? value)
    {
        if (value == null)
        {
            StringAttributes.Remove(attributeName);
        }
        else
        {
            StringAttributes[attributeName] = value;
        }
    }

    public bool TryGetString(string attributeName, out string? value)
    {
        return StringAttributes.TryGetValue(attributeName, out value);
    }

    public string GetString(string attributeName)
    {
        if (StringAttributes.TryGetValue(attributeName, out string? value))
        {
            return value;
        }
        else
        {
            throw new Exception($"Unknown attribute: {attributeName}");
        }
    }

    //----------------------------------
    // Float attributes
    //----------------------------------

    public Dictionary<string, float> FloatAttributes { get; } = new();

    /// <summary>
    /// Sets the float attribute with given <paramref name="attributeName"/> to <paramref name="value"/>
    /// if <paramref name="value"/> is different from <c>null</c>. If <paramref name="value"/> is <c>null</c>,
    /// the attribute will be removed.
    /// </summary>
    /// <param name="attributeName">name of the attribute</param>
    /// <param name="value">new value of the attribute</param>
    /// <remarks>This method will notify all listeners of this attributable</remarks>
    public void SetFloat(string attributeName, float? value)
    {
        if (value.HasValue)
        {
            FloatAttributes[attributeName] = value.Value;
            NumericAttributeNames.Add(attributeName);
        }
        else
        {
            FloatAttributes.Remove(attributeName);
        }
    }

    public float GetFloat(string attributeName)
    {
        if (FloatAttributes.TryGetValue(attributeName, out float value))
        {
            return value;
        }
        else
        {
            throw new UnknownAttribute(attributeName);
        }
    }

    public bool TryGetFloat(string attributeName, out float value)
    {
        return FloatAttributes.TryGetValue(attributeName, out value);
    }

    //----------------------------------
    // Integer attributes
    //----------------------------------

    public Dictionary<string, int> IntAttributes { get; } = new();

    /// <summary>
    /// Sets the integer attribute with given <paramref name="attributeName"/> to <paramref name="value"/>
    /// if <paramref name="value"/> is different from <c>null</c>. If <paramref name="value"/> is <c>null</c>,
    /// the attribute will be removed.
    /// </summary>
    /// <param name="attributeName">name of the attribute</param>
    /// <param name="value">new value of the attribute</param>
    /// <remarks>This method will notify all listeners of this attributable</remarks>
    public void SetInt(string attributeName, int? value)
    {
        if (value.HasValue)
        {
            IntAttributes[attributeName] = value.Value;
            NumericAttributeNames.Add(attributeName);
        }
        else
        {
            IntAttributes.Remove(attributeName);
        }
    }

    public int GetInt(string attributeName)
    {
        if (IntAttributes.TryGetValue(attributeName, out int value))
        {
            return value;
        }
        else
        {
            throw new UnknownAttribute(attributeName);
        }
    }

    public bool TryGetInt(string attributeName, out int value)
    {
        return IntAttributes.TryGetValue(attributeName, out value);
    }

    //----------------------------------
    // Numeric attributes
    //----------------------------------

    public bool TryGetNumeric(string attributeName, out float value)
    {
        if (IntAttributes.TryGetValue(attributeName, out int intValue))
        {
            value = intValue;
            return true;
        }
        else
        {
            // second try if we cannot find attributeName as an integer attribute
            return FloatAttributes.TryGetValue(attributeName, out value);
        }
    }

    /// <summary>
    /// Returns the value of a numeric (integer or float) attribute for the
    /// attribute named <paramref name="attributeName"/> if it exists.
    /// Otherwise an exception is thrown.
    ///
    /// Note: It could happen that the same name is given to a float and
    /// integer attribute, in which case the float attribute will be
    /// preferred.
    /// </summary>
    /// <param name="attributeName">name of an integer or float attribute</param>
    /// <returns>value of numeric attribute <paramref name="attributeName"/></returns>
    /// <exception cref="UnknownAttribute">thrown in case there is no such <paramref name="attributeName"/></exception>
    public float GetNumeric(string attributeName)
    {
        if (FloatAttributes.TryGetValue(attributeName, out float floatValue))
        {
            return floatValue;
        }
        else if (IntAttributes.TryGetValue(attributeName, out int intValue))
        {
            return intValue;
        }
        throw new UnknownAttribute(attributeName);
    }

    // ------------------------------
    // Range attributes
    // ------------------------------

    public const string RangeStartLineSuffix = "_StartLine";
    public const string RangeStartCharacterSuffix = "_StartCharacter";
    public const string RangeEndLineSuffix = "_EndLine";
    public const string RangeEndCharacterSuffix = "_EndCharacter";

    /// <summary>
    /// Sets the range attribute with given <paramref name="attributeName"/> to <paramref name="value"/>
    /// if <paramref name="value"/> is different from <c>null</c>. If <paramref name="value"/> is <c>null</c>,
    /// the attribute will be removed.
    /// </summary>
    /// <param name="attributeName">name of the attribute</param>
    /// <param name="value">new value of the attribute</param>
    /// <remarks>This method will notify all listeners of this attributable</remarks>
    public void SetRange(string attributeName, Range? value)
    {
        if (value == null)
        {
            IntAttributes.Remove(attributeName + RangeStartLineSuffix);
            IntAttributes.Remove(attributeName + RangeStartCharacterSuffix);
            IntAttributes.Remove(attributeName + RangeEndLineSuffix);
            IntAttributes.Remove(attributeName + RangeEndCharacterSuffix);
        }
        else
        {
            IntAttributes[attributeName + RangeStartLineSuffix] = value.StartLine;
            IntAttributes[attributeName + RangeEndLineSuffix] = value.EndLine;
            if (value.StartCharacter.HasValue)
            {
                IntAttributes[attributeName + RangeStartCharacterSuffix] = value.StartCharacter.Value;
            }
            else
            {
                IntAttributes.Remove(attributeName + RangeStartCharacterSuffix);
            }
            if (value.EndCharacter.HasValue)
            {
                IntAttributes[attributeName + RangeEndCharacterSuffix] = value.EndCharacter.Value;
            }
            else
            {
                IntAttributes.Remove(attributeName + RangeEndCharacterSuffix);
            }
        }
    }

    public bool TryGetRange(string attributeName, out Range? value)
    {
        if (IntAttributes.TryGetValue(attributeName + RangeStartLineSuffix, out int startLine) &&
            IntAttributes.TryGetValue(attributeName + RangeEndLineSuffix, out int endLine))
        {
            int? startCharacter, endCharacter;
            if (IntAttributes.TryGetValue(attributeName + RangeStartCharacterSuffix, out int startCharacterValue))
            {
                startCharacter = startCharacterValue;
            }
            else
            {
                startCharacter = null;
            }
            if (IntAttributes.TryGetValue(attributeName + RangeEndCharacterSuffix, out int endCharacterValue))
            {
                endCharacter = endCharacterValue;
            }
            else
            {
                endCharacter = null;
            }
            value = new Range(startLine, endLine, startCharacter, endCharacter);
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }

    public Range GetRange(string attributeName)
    {
        if (TryGetRange(attributeName, out Range? value))
        {
            return value!;
        }
        else
        {
            throw new UnknownAttribute(attributeName);
        }
    }

    /// <summary>
    /// Yields all string attribute names of this <see cref="Attributable"/>.
    /// </summary>
    /// <returns>all string attribute names</returns>
    public ICollection<string> AllStringAttributeNames()
    {
        return StringAttributes.Keys;
    }

    /// <summary>
    /// Yields all toggle attribute names of this <see cref="Attributable"/>.
    /// </summary>
    /// <returns>all toggle attribute names</returns>
    public ICollection<string> AllToggleAttributeNames()
    {
        return ToggleAttributes;
    }

    /// <summary>
    /// Yields all float attribute names of this <see cref="Attributable"/>.
    /// </summary>
    /// <returns>all float attribute names</returns>
    public ICollection<string> AllFloatAttributeNames()
    {
        return FloatAttributes.Keys;
    }

    /// <summary>
    /// Yields all integer attribute names of this <see cref="Attributable"/>.
    /// </summary>
    /// <returns>all integer attribute names</returns>
    public ICollection<string> AllIntAttributeNames()
    {
        return IntAttributes.Keys;
    }

    //----------------------------------
    // General
    //----------------------------------

    /// <summary>
    /// Yields true if this <see cref="Attributable"/> has exactly the same attributes
    /// as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">other <see cref="Attributable"/> to be compared to</param>
    /// <returns></returns>
    public bool HasSameAttributes(Attributable? other)
    {
        if (other == null)
        {
            return false;
        }
        else if (!toggleAttributes.SetEquals(other.toggleAttributes))
        {
            return false;
        }
        else if (!AreEqual(StringAttributes, other.StringAttributes))
        {
            return false;
        }
        else if (!AreEqual(IntAttributes, other.IntAttributes))
        {
            return false;
        }
        else if (!AreEqual(FloatAttributes, other.FloatAttributes))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Yields true if the two dictionaries are equal, i.e., have the same number of entries,
    /// and for each key in <paramref name="left"/> there is the same key in <paramref name="right"/>
    /// with the same value and vice versa.
    /// </summary>
    /// <typeparam name="V">any kind of type for a dictionary value</typeparam>
    /// <param name="left">left dictionary for the comparison</param>
    /// <param name="right">right dictionary for the comparison</param>
    /// <returns>true if <paramref name="left"/> and <paramref name="right"/> are equal</returns>
    private static bool AreEqual<V>(IDictionary<string, V> left, IDictionary<string, V> right)
    {
        return left.Count == right.Count && !left.Except(right).Any();
    }

    /// <summary>
    /// Returns a string representation for all attributes and their values for this
    /// attributable.
    /// </summary>
    /// <returns>string representation of all attributes</returns>
    public override string ToString()
    {
        string result = toggleAttributes.Aggregate("", (current, attr) => current + $" \"{attr}\": true,\n");

        result = StringAttributes.Aggregate(result, (current, attr) => current + $" \"{attr.Key}\": \"{attr.Value}\",\n");

        result = IntAttributes.Aggregate(result, (current, attr) => current + $" \"{attr.Key}\": {attr.Value},\n");

        return FloatAttributes.Aggregate(result, (current, attr) => current + $" \"{attr.Key}\": {attr.Value},\n");
    }
}
