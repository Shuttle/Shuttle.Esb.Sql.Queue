using System;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlUriParser
    {
        internal const string Scheme = "sql";

        public SqlUriParser(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            if (!uri.Scheme.Equals(Scheme, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidSchemeException(Scheme, uri.ToString());
            }

            if (uri.LocalPath == "/" || uri.Segments.Length != 2)
            {
                throw new UriFormatException(string.Format(Esb.Resources.UriFormatException,
                    "sql://{{connection-name}}/{{table-name}}",
                    uri));
            }

            Uri = uri;

            ConnectionName = Uri.Host;
            TableName = Uri.Segments[1];
        }

        public Uri Uri { get; }
        public string ConnectionName { get; }
        public string TableName { get; }
    }
}