import logging
import pyodbc 
import azure.functions as func
from azure.eventhub.aio import EventHubProducerClient
from azure.eventhub import EventData
import json
from datetime import datetime
import asyncio
import os

#logger = logging.getLogger('azure')
#logger.setLevel(logging.WARNING)

async def main(req: func.HttpRequest) -> func.HttpResponse:
    ##SQL
    conn = pyodbc.connect('Driver={SQL Server};'
                      'Server=.;'
                      'Database=StackOverflow;'
                      'Trusted_Connection=yes;')
    cursor = conn.cursor()
    cursor.execute("""SELECT (SELECT CAST([id] AS varchar(50)) AS [id]
                            ,[CreationDate]
                            ,[PostId]
                            ,[Score]
                            ,[Text]
                            ,[UserId] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS Result
                        FROM [StackOverflow].[dbo].[Comments]
                        ORDER BY [PostId] DESC""")

    ##Event Hub
    producer = EventHubProducerClient.from_connection_string(conn_str="Endpoint=sb://eventhub-app.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=yZtkyJm1v3TrN1deUrwzzrz/SwgjbvrhE5ttzfZInws="
                                                                , eventhub_name="comments-hub")

    event_data_batch = await producer.create_batch()
    i=0
    startTime = datetime.now()
    for row in cursor:
        obj = json.loads(row.Result)
        obj["ExtractionDate"] = datetime.now().isoformat()
        event_data_batch.add(EventData(str(obj)))

        i=i+1
        if i%5 == 0:
            await producer.send_batch(event_data_batch)
            event_data_batch = await producer.create_batch()
        if i%100==0:
            logging.warning("{} elapse for the last 100 Comments insertions".format(datetime.now()-startTime))
            startTime = datetime.now()

    
    return func.HttpResponse(
            "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.",
            status_code=200
    )
