using System;
using System.IO;

namespace PodFetch
{
    public class Link
    {
        public Link(string url, LinkKind kind)
        {
            Uri = new Uri(url, UriKind.Absolute);
            Kind = kind;
        }

        public Uri Uri { get; private set; }
        public LinkKind Kind { get; private set; }

        public string GetFileName(string basePath)
        {
            return new DirectoryInfo(Path.Combine(basePath, 
                Path.GetFileName(Uri.LocalPath))).FullName;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", Kind, Uri);
        }
    }
}
