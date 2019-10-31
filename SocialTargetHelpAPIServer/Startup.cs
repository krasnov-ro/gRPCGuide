using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocialTargetHelpAPIServer
{
    class Startup
    {
    }

    //public class ConnectionStringSettings : IConnectionStringSettings
    //{
    //    public string ConnectionString { get; set; }
    //    public string Name { get; set; }
    //    public string ProviderName { get; set; }
    //    public bool IsGlobal => false;
    //}

    //public class MySettings : ILinqToDBSettings
    //{
    //    public IEnumerable<IDataProviderSettings> DataProviders => Enumerable.Empty<IDataProviderSettings>();

    //    public string DefaultConfiguration => "SqlServer";
    //    public string DefaultDataProvider => "SqlServer";

    //    public IEnumerable<IConnectionStringSettings> ConnectionStrings
    //    {
    //        get
    //        {
    //            yield return
    //                new ConnectionStringSettings
    //                {
    //                    Name = "Npgsql",
    //                    ProviderName = "Npgsql",
    //                    ConnectionString = @"Server=.\;Database=Northwind;Trusted_Connection=True;Enlist=False;"
    //                };
    //        }
    //    }
    //}
}
