using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NodaTime;
using Raven.Client.Documents.Conventions;
using Raven.Client.NodaTime;
using Raven.Embedded;
using Xunit;
using Flurl;
using Flurl.Http;
using Raven.Client.Documents.Operations;
using RavenDB.Issues.RavenDB_16042.Common;

namespace RavenDB.Issues.RavenDB_16042.v4_2
{
    public sealed class EmbeddedServerTests
    {
        public sealed class AutoIndexTests
        {
            [Fact]
            public void PutNewDocumentsAndQueryBy3Fields_ReturnsAllOfThem()
            {
                // arrange
                var dataDirectory = ArrangeUtils.PrepareDataDirectory();
                var serverUrl = "http://127.0.0.1:8080/";
                var databaseName = "Test_1";
                var serverOptions = new ServerOptions
                {
                    DataDirectory = dataDirectory,
                    ServerUrl = serverUrl
                };
                var conventions = new DocumentConventions();
                conventions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                var databaseOptions = new DatabaseOptions(databaseName)
                {
                    Conventions = conventions
                };
                EmbeddedServer.Instance.StartServer(serverOptions);

                // quick sanity check that proper db is in place and it is in initial state
                var documentStore = EmbeddedServer.Instance.GetDocumentStore(databaseOptions);
                var stats = documentStore.Maintenance.Send(new GetCollectionStatisticsOperation());
                var initialDocuments = documentStore.OpenSession().Advanced.RawQuery<dynamic>("from FeedingTask").ToList();
                if (initialDocuments.Count != 49 || stats.CountOfDocuments != 129)
                {
                    throw new InvalidOperationException($"Rollback the database {databaseName} to initial state");
                }

                // act
                // put 4 documents
                foreach (var document in TestData.DocumentsToPut)
                {
                    serverUrl
                        .AppendPathSegments("databases", databaseName, "docs")
                        .SetQueryParam("id", document.Key)
                        .PutStringAsync(document.Value)
                        .GetAwaiter().GetResult();
                }

                // query put documents by ExecutionDate, Status and MixerId fields
                var result = serverUrl
                    .AppendPathSegments("databases", databaseName, "queries")
                    .PostStringAsync(
                        "{\"Query\":\"from FeedingTask where ExecutionDate = \\\"2020-10-28\\\" AND Status IN (\\\"Created\\\") AND MixerId = \\\"Mixer/1\\\"\"}")
                    .GetAwaiter().GetResult();

                // assert that all documents were queried
                var responseBody = result.GetJsonAsync().GetAwaiter().GetResult();
                Assert.Equal(4, responseBody.Results.Count);
            }
        }
    }
}
