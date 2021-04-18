import logging
import pyodbc 
import azure.functions as func
from azure.cosmos import CosmosClient,PartitionKey
import json
from datetime import datetime

import os

logger = logging.getLogger('azure')
logger.setLevel(logging.WARNING)

def main(req: func.HttpRequest) -> func.HttpResponse:
    ##SQL
    conn = pyodbc.connect('Driver={SQL Server};'
                      'Server=.;'
                      'Database=StackOverflow;'
                      'Trusted_Connection=yes;')
    cursor = conn.cursor()
    cursor.execute("""SELECT (SELECT CAST([id] AS varchar(50)) AS [id]
                        ,[AcceptedAnswerId]
                        ,[AnswerCount]
                        ,[Body]
                        ,[ClosedDate]
                        ,[CommentCount]
                        ,[CommunityOwnedDate]
                        ,[CreationDate]
                        ,[FavoriteCount]
                        ,[LastActivityDate]
                        ,[LastEditDate]
                        ,[LastEditorDisplayName]
                        ,[LastEditorUserId]
                        ,[OwnerUserId]
                        ,[ParentId]
                        ,[PostTypeId]
                        ,[Score]
                        ,[Tags]
                        ,[Title]
                        ,[ViewCount]
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS Result
                    FROM [StackOverflow].[dbo].[Posts] ORDER BY [id] ASC""")

    ##Cosmos
    client = CosmosClient(os.environ["ACCOUNT_URI"], os.environ["ACCOUNT_KEY"], logging_enable=False)
    database_name = 'StackOverflow'
    database = client.create_database_if_not_exists(id=database_name)
    container_name = 'Posts'
    container = database.create_container_if_not_exists(
        id=container_name, 
        partition_key=PartitionKey(path="/id"),
        offer_throughput=400,
        analytical_storage_ttl=-1
    )
    i = 0
    startTime = datetime.now()
    for row in cursor:
        obj = json.loads(row.Result)
        obj["ExtractionDate"] = datetime.now().isoformat()
        container.create_item(obj,False)
        
        i=i+1
        if i%100==0:
            logging.warning("{} elapse for the last 100 Posts insertions".format(datetime.now()-startTime))
            startTime = datetime.now()
    
    return func.HttpResponse(
            "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.",
            status_code=200
    )
