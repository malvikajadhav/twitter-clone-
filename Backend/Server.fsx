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
