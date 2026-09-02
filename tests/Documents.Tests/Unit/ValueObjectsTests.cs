namespace Documents.Tests.Unit;
public class ValueObjectsTests { [Fact] public void ContentHash_Create_Valid() { var ok = Domain.ValueObjects.ContentHash.Create(new string('a',64)); Assert.True(ok.IsSuccess); } }
