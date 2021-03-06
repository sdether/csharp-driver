﻿using System;
using System.Collections.Generic;

namespace Cassandra
{
    internal partial class CassandraConnection : IDisposable
    {
        public IAsyncResult BeginExecuteQueryCredentials(IDictionary<string, string> credentials, AsyncCallback callback, object state, object owner)
        {
            return BeginJob(callback, state, owner, "CREDENTIALS", new Action<int>((streamId) =>
            {
                Evaluate(new CredentialsRequest(streamId, credentials), streamId, new Action<ResponseFrame>((frame2) =>
                {
                    var response = FrameParser.Parse(frame2);
                    if (response is ReadyResponse)
                        JobFinished(streamId, new OutputVoid(null));
                    else
                        _protocolErrorHandlerAction(new ErrorActionParam() { AbstractResponse = response, StreamId = streamId });

                }));
            }));
        }

        public IOutput EndExecuteQueryCredentials(IAsyncResult result, object owner)
        {
            return AsyncResult<IOutput>.End(result, owner, "CREDENTIALS");
        }

        public IOutput ExecuteCredentials(IDictionary<string, string> credentials)
        {
            return EndExecuteQueryCredentials(BeginExecuteQueryCredentials(credentials, null, null, this), this);
        }
    }
}
