﻿using System;

namespace Cassandra
{
    internal partial class CassandraConnection : IDisposable
    {
        public IAsyncResult BeginPrepareQuery(string cqlQuery, AsyncCallback callback, object state, object owner)
        {
            return BeginJob(callback, state, owner, "PREPARE", new Action<int>((streamId) =>
            {
                Evaluate(new PrepareRequest(streamId, cqlQuery), streamId, new Action<ResponseFrame>((frame2) =>
                {
                    var response = FrameParser.Parse(frame2);
                    if (response is ResultResponse)
                        JobFinished(streamId, (response as ResultResponse).Output);
                    else
                        _protocolErrorHandlerAction(new ErrorActionParam() { AbstractResponse = response, StreamId = streamId });

                }));
            }));
        }

        public IOutput EndPrepareQuery(IAsyncResult result, object owner)
        {
            return AsyncResult<IOutput>.End(result, owner, "PREPARE");
        }

        public IOutput PrepareQuery(string cqlQuery)
        {
            return EndPrepareQuery(BeginPrepareQuery(cqlQuery, null, null, this), this);
        }
    }
}
