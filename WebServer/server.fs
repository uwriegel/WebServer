module Server
open System
open System.Net
open System.Net.Sockets
open RequestSession

// TODO: TLS

// TODO: async nur bis Daten da, keine Verschachtelten async-Blöcke in der SocketSession
// TODO: Asynchrones rekursives Einlesen, bis entweder read = 0 oder Header-EndIndex gesetzt

type Server = {
    start: unit->unit
    stop: unit->unit
    configuration: Configuration.Value
}

let private onConnected tcpClient configuration = 
    try
        create tcpClient configuration |> ignore
    with
    | :? SocketException as se when se.NativeErrorCode = 10054
        -> ()
    | :? ObjectDisposedException 
        -> ()
    | ex -> printfn "Error in asyncOnConnected occurred: %s" <| ex.ToString () 


let rec beginConnect (listener: TcpListener) configuration = 
    listener.BeginAcceptTcpClient (fun a ->
        try
            let client = listener.EndAcceptTcpClient a 
            onConnected client configuration
            beginConnect listener configuration
        with
        | :? SocketException as se when se.SocketErrorCode = SocketError.Interrupted 
            -> printfn "Stopping listening..."
        | ex -> printfn "Could not stop HTTP Listener: %s" <|ex.ToString () 
    , null) |> ignore

let private start (listener: TcpListener, configuration: Configuration.Value) () = 
    try
        printfn "Starting HTTP Listener..."
        // Ansonsten kann nach Beenden des Listeners für 2 min kein neuer gestartet werden!
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true)    
        listener.Start ()
        beginConnect listener configuration
        printfn "HTTP Listener started"
    with 
    | ex -> 
        printfn "Could not start HTTP Listener: %s" <|ex.ToString ()
        raise ex

let private stop (listener: TcpListener) () = 
    try
        printfn "Stopping HTTP Listener..."
        listener.Stop ()
        printfn "HTTP Listener stopped"
    with 
        | ex -> printfn "Could not stop HTTP Listener: %s" <|ex.ToString ()

let create (configuration: Configuration.Value) = 
    printfn "Initializing Server..."
    //ServicePointManager.SecurityProtocol = 10000 |> ignore
    printfn "Domain name: %s" configuration.DomainName
    if configuration.LocalAddress <> IPAddress.Any then 
        printfn "Binding to local address: %s" <| configuration.LocalAddress.ToString ()

    printfn "Listening on port %d" configuration.Port
    let result = IPV6ListenerFactory.create configuration.Port
    let listener = result.Listener
    if not result.Ipv6 then 
        printfn "IPv6 or IPv6 dual mode not supported, switching to IPv4"

    printfn "Server initialized"
    {
        start = start (listener, configuration)
        stop = stop listener
        configuration = configuration
    }
    

