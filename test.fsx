// Create a credentials file based on creds.fsx and #load it here
#load "creds-private.fsx"

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Host

open CamdenTown.Attributes
open CamdenTown.FunctionApp

// Here we set up our Azure Function App
let app = Creds.App()

let numbers = [ for i in 1..10 -> i * i ]

[<HttpTrigger>]
let HttpClosure(req: HttpRequestMessage, log: TraceWriter) =
  async {
    log.Error(sprintf "HttpClosure: %s" (req.RequestUri.ToString()))
    let! body =
      if not (isNull req.Content) then
        req.Content.ReadAsStringAsync() |> Async.AwaitTask
      else
        async { return "" }

    let content =
      new StringContent(
        sprintf
          """
{
  "method": "%s",
  "uri": "%s",
  "body": "%s",
  "numbers": [%s]
}
"""
          req.Method.Method
          (req.RequestUri.ToString())
          body
          (numbers |> List.map string |> String.concat ", "),
        Text.Encoding.UTF8,
        "application/json"
      )
    let resp = new HttpResponseMessage(HttpStatusCode.OK)
    resp.Content <- content
    return resp
  } |> Async.StartAsTask

[<Queue>]
let queue1 = "queue1"

[<Queue>]
let queue2 = "queue2"

[<CLIMutable>]
type Foo = {
  Name: string
  Food: string
}

[<QueueTrigger(typeof<Foo>, queue1)>]
[<QueueResult(typeof<Foo>, queue2)>]
let QueueHandler(input: Foo, log: TraceWriter) =
  async {
    log.Error(sprintf "%s likes %s" input.Name input.Food)
    return { Name = "Bob " + input.Name; Food = input.Food + " and cheese" }
  } |> Async.StartAsTask

[<QueueTrigger(typeof<Foo>, queue2)>]
let QueueSecond(input: Foo, log: TraceWriter) =
  log.Error(sprintf "%s likes %s" input.Name input.Food)

[<TimerTrigger("*/10 * * * * *")>]
let TimerToLog(timer: TimerInfo, log: TraceWriter) =
  log.Error(sprintf "Executed at %s" (DateTimeOffset.Now.ToString()))

// TableIn, TableOut, BlobTrigger, BlobIn, BlobOut


// TODO:
// handle auth token expiration
// create service principal
//  https://azure.microsoft.com/en-gb/documentation/articles/resource-group-authenticate-service-principal-cli/
//  ~/.azure has access tokens and subscription info after a CLI login
// create needed queues
//  statically define them to avoid string referencing?
// bind a storage account
// set log level on webapp
// stream log

app.Deploy [ QueueHandler; QueueSecond ]
app.Deploy [ TimerToLog ]
app.Deploy [ HttpClosure ]
app.Undeploy [ TimerToLog ]
app.Undeploy [ HttpClosure ]
app.Restart()
app.Delete()
