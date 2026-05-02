namespace DrawnUi.Draw
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ContentPropertyAttribute : Attribute
    {
        public ContentPropertyAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}