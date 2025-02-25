namespace Application.Features.Categories.Consts;

public static class CategoriesBusinessMessages
{
    public const string CategoryNotExists = "Category not exists.";
    public const string CategoryNameAlreadyExists = "Category name already exists.";
    public const string ParentCategoryShouldNotBeSelf = "Parent category should not be self.";
    public const string ParentCategoryShouldNotBeChild = "Parent category should not be child.";
    public const string SubCategoryShouldNotBeParent = "Sub category should not be parent.";
    public const string ParentCategoryShouldNotBeDescendant = "Parent category should not be descendant.";
    public const string ParentCategoryShouldBeNullWhenUpdate = "Parent category should be null when update.";
    
    
}