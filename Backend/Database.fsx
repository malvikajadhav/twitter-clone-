#r "nuget: SQLite.Interop.dll, 1.0.103"
#r "nuget: System.Data.SQLite.Core, 1.0.115.5"
#r "nuget: FSharp.Data.Dapper, 2.0.0"

open System.Data.SQLite
open FSharp.Data.Dapper

// Defining 
let [<Literal>] DbFileName = "project4part2.db"

module Connection = 
    let private diskConnectionString (dataSource : string) = 
        sprintf "Data Source = %s;" dataSource

    let dbDisk () = new SQLiteConnection(diskConnectionString DbFileName)

module Types = 
    [<CLIMutable>]
    type User = {
        id : int
        username: string
        password: string
    }

    [<CLIMutable>]
    type Tweet = {
        id : int
        username : string
        tweet : string
    }

    [<CLIMutable>]
    type Follow = {
        id : int
        username : string
        following : string
    }



module Db = 
    let private connection () = Connection.SqliteConnection(Connection.dbDisk())
    let querySeqAsync<'R> = querySeqAsync<'R> (connection)
    let querySingleAsync<'R> = querySingleOptionAsync<'R> (connection)

    module Schema = 
        let createTables = querySingleAsync<int> {
            script """
                CREATE TABLE IF NOT EXISTS Users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username VARCHAR(255) NOT NULL,
                    password VARCHAR(255) NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Tweets (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username VARCHAR(255) NOT NULL,
                    tweet VARCHAR(255) NOT NULL
                );
                DROP TABLE IF EXISTS Follows;
                CREATE TABLE Follows (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username VARCHAR(255) NOT NULL,
                    following VARCHAR(255) NOT NULL
                );
            """
        }
        
    module User = 

        let Insert username password = querySingleAsync<int> {
            script "INSERT INTO Users (username, password) VALUES (@username, @password)"
            parameters (dict ["username", box username; "password", box password])
        }

        let Get username = querySingleAsync<Types.User> {
            script "SELECT * FROM Users WHERE username = @username LIMIT 1"
            parameters (dict ["username", box username])
        }


        // let GetAll() = querySeqAsync<Types.User> { script "SELECT * FROM Users" } 

   
    module Tweet = 

        let Insert username tweet = querySingleAsync<int> {
            script "INSERT INTO Tweets (username, tweet) VALUES (@username, @tweet)"
            parameters (dict ["username", box username; "tweet", box tweet])
        }

        let GetLatestTweets() username = querySeqAsync<Types.Tweet> {
            script "SELECT * FROM Tweets WHERE (username=@username) ORDER BY id DESC LIMIT 10"
            parameters (dict ["username", box username])
        }
        
        let GetQuery() = querySeqAsync<Types.Tweet> { script ("SELECT * FROM Tweets")} 