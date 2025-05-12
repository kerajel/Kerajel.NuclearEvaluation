namespace ODataStringToDynamicLinq.Tests;

using System.Linq;
using Community.OData.Linq;
using Xunit;

public sealed class ODataDynamicLinqTests
{
    [Fact]
    public void FilterByNameAndAge_WithODataString_AppliesCorrectWhereClause()
    {
        // Arrange
        IQueryable<Person> people = new Person[]
        {
            new(1, "Alice",   30, new Address("London",    "Baker Street")),
            new(2, "Bob",     25, new Address("Paris",     "Champs-Élysées")),
            new(3, "Alice",   35, new Address("New York",  "5th Ave"))
        }.AsQueryable();

        // Act
        Person[] result = people
            .OData()
            .Filter("Name eq 'Alice' and Age gt 30")
            .ToArray();

        // Assert
        Assert.Single(result);
        Assert.Equal(3, result[0].Id);
    }

    [Fact]
    public void FilterAndOrderByNameAndAge_WithODataString_AppliesCorrectFilteringAndOrdering()
    {
        // Arrange
        IQueryable<Person> people = new Person[]
        {
            new(1, "Alice", 30, new Address("London", "Baker Street")),
            new(2, "Alice", 25, new Address("Paris",  "Rue de Rivoli")),
            new(3, "Bob",   40, new Address("London", "Fleet Street"))
        }.AsQueryable();

        // Act
        Person[] result = people
            .OData()
            .Filter("Name eq 'Alice'")
            .OrderBy("Age desc")
            .ToArray();

        // Assert
        Assert.Equal(2, result.Length);
        Assert.Equal(new[] { 1, 2 }, result.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void FilterByAgeAndOrderByNameThenAge_AppliesComplexFilterAndMultiLevelOrdering()
    {
        // Arrange
        IQueryable<Person> people = new Person[]
        {
            new(1, "Alice",   30, new Address("London",   "Baker Street")),
            new(2, "Bob",     25, new Address("Paris",    "Champs-Élysées")),
            new(3, "Charlie", 35, new Address("London",   "Fleet Street")),
            new(4, "Bob",     35, new Address("New York", "5th Ave"))
        }.AsQueryable();

        // Act
        Person[] result = people
            .OData()
            .Filter("Age ge 30")
            .OrderBy("Name asc, Age desc")
            .ToArray();

        // Assert
        Assert.Equal(new[] { 1, 4, 3 }, result.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void FilterByCityAndOrderByStreet_OnNestedAddressProperties()
    {
        // Arrange
        IQueryable<Person> people = new Person[]
        {
            new(1, "Alice",   30, new Address("London", "Abbey Road")),
            new(2, "Bob",     25, new Address("London", "Baker Street")),
            new(3, "Charlie", 35, new Address("Paris",  "Rue de Rivoli")),
            new(4, "Diana",   40, new Address("London", "Fleet Street"))
        }.AsQueryable();

        // Act
        Person[] result = people
            .OData()
            .Filter("Address/City eq 'London'")
            .OrderBy("Address/Street asc")
            .ToArray();

        // Assert
        Assert.Equal(3, result.Length);
        Assert.Equal(new[] { 1, 2, 4 }, result.Select(p => p.Id).ToArray());
    }
}

public sealed record Address(string City, string Street);

public sealed record Person(int Id, string Name, int Age, Address Address);
