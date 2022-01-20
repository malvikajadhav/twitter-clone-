#r "nuget: Akka, 1.4.25"
#r "nuget: Akka.FSharp, 1.4.25"
#r "nuget: Newtonsoft.Json, 13.0.1"
#r "nuget: System.Data.SQLite.Core, 1.0.115.5"
#r "nuget: FSharp.Data.Dapper, 2.0.0"
#r "nuget: Suave, 2.6.1"
#load "Database.fsx"

// Initializing Libraries
open Akka.FSharp
open System
open System.IO
open Newtonsoft.Json
open System.Data.SQLite
open FSharp.Data.Dapper
open Database
open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open Suave.Writers
open Suave.RequestErrors
open Suave.Files
open Suave.Logging
open Suave.WebSocket
open Suave.Sockets.Control.SocketMonad
open Database.Types

type FeedRecord = {
    task : string
    username : string
    value : string
}

type RequestResponse = {
    message: string
    value: string
}

type UserSignOutType = {
    user : string
}

type QueryResponse = {
    message : string
    result : Tweet array
}

type TweetRecord = {
    username: string
    tweet: string
}

type FollowRecord = {
    username: string
    following: string
}


type UserRecord = {
    username : string
    password : string
}

type FollowTweetRecord = {
    user : string
    follower : string
    tweet : string
}

type QueryRecord = {
    user : string
    query : string
}

type FeedSend = 
| Init of (FeedRecord * WebSocket)
| Tweet of (FeedRecord)
| Follow of (string*string)
| FollowTweet of (FollowTweetRecord)
| CloseSocket of (string)
| DeleteLogin of (string)

let system = System.create "tweet" <| Configuration.load()

let getBytes (msg : string) = 
    let str = JsonConvert.SerializeObject msg
    str
    |> System.Text.Encoding.ASCII.GetBytes
    |> Sockets.ByteSegment

let agent = MailboxProcessor<string* WebSocket>.Start(fun inbox ->
  let rec messageLoop() = async {
    let! msg,webSkt = inbox.Receive()
    let byteRes = getBytes msg
    let! _ = webSkt.send Text byteRes true
    return! messageLoop()
  }
  messageLoop()
)

let liveFeedActor (mailbox:Actor<_>) =
    let mutable activeUsers = Map.empty
    let mutable feedTable = Map.empty
    let rec messageLoop () =
        actor {
            let! msg = mailbox.Receive()
            match msg with
            | Init(message, wbSocket) ->
                activeUsers <- Map.add message.username wbSocket activeUsers
            | Tweet(message) ->
                let str =  "You tweeted '" + message.value + "'"
                // feedTable <- Map.add message.username str feedTable
                agent.Post (str, activeUsers.[message.username])
            | Follow(u,f) ->
                let str = u + " just started following you."
                agent.Post(str, activeUsers.[f])
            | FollowTweet(fr) ->
                let str = fr.user + " just tweeted '" + fr.tweet + "'"
                agent.Post(str, activeUsers.[fr.follower])
            | DeleteLogin(u) ->
                activeUsers <- Map.remove u activeUsers
            | _ -> ()
            return! messageLoop ()
        }
    messageLoop ()
// Spawn the feed Actor
let liveFeedRef = spawn system "feeder" liveFeedActor


let runServer argv =
    // Defining IP and port of server where we want to serve.
    // let serverIP = "192.168.0.186"
    // let serverPort = 8050

    // // App config with server IP and port
    // let serverConfig = { 
    //     defaultConfig with
    //         bindings = [ HttpBinding.createSimple HTTP serverIP serverPort]
    // }

    // Connect to database and create the tables
    Db.Schema.createTables |> Async.RunSynchronously |> ignore

    // Start the server using Suave
    startWebServer {defaultConfig with 
                        homeFolder = Some (Path.GetFullPath "./frontend")
                        logger = Targets.create Verbose [||]} argv

// Function to convert into an Object from JSON
let jsonToObject<'t> json =
        JsonConvert.DeserializeObject(json, typeof<'t>) :?> 't

// Function to create response in JSON Format
let responseJson data =
    JsonConvert.SerializeObject data 
    |> OK
    >=> setMimeType "application/json; charset=utf-8"


// Function to extract data from client request 
let resourceFromRequest<'t> (jsonReq : HttpRequest) = 
    let getString (rawForm:byte[]) = System.Text.Encoding.UTF8.GetString(rawForm)
    jsonReq.rawForm |> getString |> jsonToObject<'t>


// Fucntion to register User to Database
let registerUser (user : UserRecord) =
    match Db.User.Get user.username |> Async.RunSynchronously with
    | Some(data) ->
        {message="fail"; value="User already exists!"}
    | None ->
        Db.User.Insert user.username user.password |> Async.RunSynchronously |> ignore
        {message="success"; value="User registered!"}  


// Function to login the user if he/she exists in the database
let loginUser (user : UserRecord) =
    match Db.User.Get user.username |> Async.RunSynchronously with
    | Some(data) -> 
        if data.password= user.password then
            {message="success"; value="Login successful!"}
        else
            {message="fail"; value="Password Incorrect. Try Again!"}
    | None -> 
        {message="fail"; value="User does not exixts!"}


let userTweet (tweet : TweetRecord) =
    let value = tweet.tweet
    match Db.User.Get tweet.username |> Async.RunSynchronously with
    | Some(user) ->
        Db.Tweet.Insert user.username value |> Async.RunSynchronously |> ignore
        let followers = Db.Follow.GetFollowers() tweet.username |> Async.RunSynchronously |> Array.ofSeq
        liveFeedRef <! Tweet({task="tweeted"; username=tweet.username; value=tweet.tweet})
        for i in [0 .. followers.Length-1] do
            liveFeedRef <! FollowTweet({user=tweet.username; follower=followers.[i].username; tweet=tweet.tweet})
        {message="success"; value="Tweet successfully registered."}
    | None ->
        {message="fail"; value="Tweet did not register!"}


let followUser (follow : FollowRecord) = 
    let user = follow.username
    let following = follow.following
    match Db.User.Get following |> Async.RunSynchronously with
    | Some(u) ->
        match Db.Follow.GetFollowing user following |> Async.RunSynchronously with
        | Some(f) ->
            let rep =  "You are already following " + following
            {message="fail"; value=rep}
        | None ->
            Db.Follow.Insert user following |> Async.RunSynchronously |> ignore
            let rep = "You are now following " + following 
            liveFeedRef <! Follow(user,following)
            {message="success"; value=rep}
    | None ->
        let rep = following + " does not exists."
        {message="fail"; value=rep}

let queryDb (query : QueryRecord) = 
    let mutable matchedTweets = [||]
    let tweets = Db.Tweet.GetQuery() |> Async.RunSynchronously |> Array.ofSeq
    let tweetL = tweets.Length
    for i in [0 .. tweetL-1] do
        let tweet = tweets.[i].tweet
        match tweet.Contains(query.query) with
        | true -> 
            matchedTweets <- Array.append matchedTweets [|tweets.[i]|]
        | false -> ()
    if matchedTweets.Length = 0 then
        {message="fail"; result= matchedTweets}
    else
        {message="success"; result= matchedTweets}

let userSignOut (user: UserSignOutType) = 
    liveFeedRef <! DeleteLogin(user.user)
    {message="success"; value="User Successfully Logged off!"}


let sendFeed (ws: WebSocket) (context : HttpContext) = 
    let mutable currentUser = ""
    let rec loop() =
        socket { 
            let! msg = ws.read()
            match msg with
            | (Text, data, true) ->
                let reqMsg = UTF8.toString data
                let parsed = JsonConvert.DeserializeObject<FeedRecord> reqMsg
                currentUser <- parsed.username
                liveFeedRef <! Init(parsed, ws)
                return! loop()
            | (Close, _, _) ->
                printfn "Closed WEBSOCKET"
                liveFeedRef <! CloseSocket(currentUser)
                let emptyResponse = [||] |> Sockets.ByteSegment
                do! ws.send Close emptyResponse true
            | _ -> return! loop()
        }
    loop()


// Route endpoints
let webPart : WebPart = 
    choose [
        path "/livefeed" >=> handShake sendFeed
        GET 
        >=> setHeader "Access-Control-Allow-Origin" "*"
        >=> setMimeType "application/json; charset=utf-8"
        >=> choose [
            path "/" >=> browseFileHome "index.html"
            path "/register" >=> browseFileHome "register.html"
            path "/login" >=> browseFileHome "login.html"
            browseHome
        ]
        POST
        >=> choose [
            path "/register" >=> request (resourceFromRequest<UserRecord> >> registerUser >> responseJson)
            path "/login" >=> request (resourceFromRequest<UserRecord> >> loginUser >> responseJson)
            path "/tweet" >=> request (resourceFromRequest<TweetRecord> >> userTweet >> responseJson)
            path "/follow" >=> request (resourceFromRequest<FollowRecord> >> followUser >>  responseJson)
            path "/query" >=> request (resourceFromRequest<QueryRecord> >> queryDb >> responseJson)
            path "/signout" >=> request (resourceFromRequest<UserSignOutType> >> userSignOut >> responseJson)
        ]
        NOT_FOUND "Page not found!"
    ]

// Starting the server
runServer webPart