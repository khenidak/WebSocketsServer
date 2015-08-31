#WebSocket Server for Owin (& Azure Service Fabric) #

This repo contains components that implement a Web Socket Server (.NET) for Owin. The web socket server uses Owin Startup routine, allowing you to have additional Owin stages before or after the web socket server stage. The web socket server contains a session manager that allows hosting process to interact with active web socket sessions (for example server side broadcast messages). The sessions are implemented on top of a sequential queue (per each) that surfaces granular control on messages going to the down stream (details discussed [here](http://henidak.com/2015/08/web-socket-p1/)). 

This further extended for Azure Service Fabric specific hosting. 



## Using it with Owin##
**Typical Usage Pattern**

1. Create your socket types (subclass-ing *WebSocketSessionBase*). Each represents a session type. For example "Customer". They can expose business logic specific methods such as SendComplete each result into one or more messages send to the down stream. 
2. You can also expose server side messages that *OnReceiveAsync* can route to.
2. Optionally implement a session manager (subclass-ing *WebSocketSessionManager<t>*) or use the out of the box manager *WebSocketSessionManager* (Example: please refer to the code below for a custom manager that maps different socket types to different Owin routes)  
3. Call one of the many Owin extension methods.

The below code uses the web socket server with a self hosted Owin server

	
	 _echoFactory = new TestWebSocketSessionFactory();

     IDisposable server = WebApp.Start(_serverAddress, app =>
                {
                    	app.MapWebSocket<WebSocketSessionManager<TestWebSocketSession>, 	TestWebSocketSession>(_echoFactory);
                }
            );

	

> Check the unit test project for the complete sample code. 


- The Session Manager (referred to as factory in test project) contains a reference to all active sockets currently connected. Server can interact with the via *GetSession(predicate Func)* method. For example a server process - which hosts the web socket server - can find all active sessions matching a certain criteria then call methods to send messages on the downstream. For further details check the code below.
- Socket session class implements a Post & a Send methods. Send puts the message at the beginning of the downstream queue while Post puts it at the end. Both depends on the custom task scheduler discussed below. 
- Socket Session & Session Manager implement IDisposable and will close sessions(s) when they are GCed or Disposed.
- Socket session implement close (async), abort (sync) and DrainAndClose which waits for the messages on the downstream (prior to the drain event) to be sent to connected client before closing the session.
- The package contains a custom Owin middle-ware and extension methods that allows you to quickly integrate it in your Owin pipeline. 


### Interacting with Connected Web Sockets###

You can use the SessionManager instance to interact with all the connected web sockets that was created by the manager (as a result of Owin pipeline calls) as the following

	
	//m_Manager is a session manager attached to Owin as the above code. 
	// this returns all sockets 
	m_Manager.GetSession((session) => true);

	// GeneralWSSession is a class that implements WebSocketSessionBase
    reach (var client in clients)
         await ((GeneralWSSession)client).SayHelloToGeneral(string.Format("To all general - {0}", DateTime.UtcNow.Ticks));

	

## Using it with Service Fabric ##
WebSocketServer.ServiceFabric.Services library contain classes that you need to run the web socket server in context of Service Fabric. The session manager Service Fabric uses enables you manage sockets of different types (mapped to different addresses). 

**Typical Usage Pattern (Services Side):** 

- Create classes that implements your web socket (subclass-ing ServiceFabricSocketSessionBase class). For example i have 3 General, Customer & Order each implements a different socket. 
>You can also map just one socket implementation. 

- Use WebSocketCommunicationListener in your service as the following  

	

       protected override ICommunicationListener CreateCommunicationListener()
        {
            m_listener = new WebSocketCommunicationListener(StateManager);
            // map to any type that implements ServiceFabricWebSocketSessionBase
            m_listener.Map("customer", typeof(CustomerWSSession)); // mapped to <listeningaddress>/customer
            m_listener.Map("order", typeof(OrderWSSession));    // mapped to <listeningaddress>/order
            m_listener.Map("", typeof(GeneralWSSession));    // mapped to <listeningaddress>/


            // you can use the above to filter sockets based on the replica type
            // for example primaries can have different socket types than seconaries. 


            // Listening address is what the server actually listen to
            // publishing address is what is returned to Fabric runtime (commnunicated as EndPoint.Address on client side)
            /*
                if you want to control how listening and publishing addresses are created
                    m_listener.OnCreateListeningAddress = (listener) => { << return my listening address here >>}
                    m_listener.OnCreatePublishingAddress = (listener) => { << return my Publishing ddress here >>}            
            */

            /* 
                if you want to add more OWIN stuff pre or post web socket stages
                    m_listener.OnOwinPreMapping = (listener, appbuilder) => { << appbuilder.UseXX  >>}
                    m_listener.OnOwinPostMapping = (listener, appbuilder) => { << appbuilder.UseXX  >>}
            */


            return m_listener;
        }


	

**Notes**
- The listener injects Service's IReliableStateManager into the sockets. This enables you to call reliable collections in your sockets.  

- The listener also exposes Owin Startup to you so you can use *OnOwinPreMapping* and *OnOwinPostMapping* method to wire up anyother custom Owin middleware (for example wiring up ADAL or a custom request logger). 

- The listener maintains a reference to the Session Manager where you can access it from anywhere in your service (for example in OnRunAsync method) as the following:

	

      // m_listener is an instance of WebSocketCommunicationListener 

	  // the predicate below can be anything that filters sessions
		var clients = m_listener.SessionManager.GetSession((session) => null != (session as GeneralWSSession));

        foreach (var client in clients)
            await ((GeneralWSSession)client).SayHelloToGeneral(string.Format("To all general - {0}", DateTime.UtcNow.Ticks));
        

	 

- Service Fabric extends the default session manager to implement a multi type session manager.  

> check the TestStatefulSvc project for the complete sample code.  


**Typical Usage Pattern (Client Side):** 

On the client side you can use standard ClientWebSocket (of System.Net.WebSockets namespace). You can use them using standard service resolution approach or you can use *ServiceFabricWebSocketClient* & *ServiceFabricWebSocketClientFactory* together they implement Service Fabric *ICommunicationClient*  & *CommunicationClientFactoryBase*. Those have the following Characterstics:
 

1. The client implements IDisposable and closes all the connected sockets when disposing. 
2. The client can switch socket type via GetSocket method. 
3. The client makes sure that only one socket of each type is connected (and will recycle sockets between GetScoket calls, no new sockets will be created). 

## Of Interest: Sequential Multi Q Task Scheduler ##
In order to allow ordered messages on the down stream. The web socket server sets on top of a custom task scheduler. The task scheduler allows the following: 

1. Execute Tasks in order per q. 
2. One scheduler can manage multiple number of queues.
3. Put task at the end of the queue or at the Beginning of the queue (low priority vs high priority).
4. Because it is built using the .NET TPL you can use standard await constructs. 



**Typical Usage:** 

	
		LeveledTask<long> lt = new LeveledTask<long>( () =>
                {
                    
                    return DateTime.Now.Ticks;
                });

                lt.QueueId = "Q01"; // which queue this task belongs to
                lt.IsHighPriority = true; // put at the beginning of the q
                lt.Start(scheduler);
            
		await lt; 


There is also LeveledTask for tasks that does not return results. 

> Please review TaskScheduler.Tests.cs in WebSocketServer.Tests project for further samples.


#What is in the Package#

1. *WebSocketServer* Project contains the implementation of Web Socket Server on Owin.
2. *WebSocketServer.Utils* contains the implementation of the Custom Task Scheduler (can be used on its own).
3. *WebSocketServer.ServiceFabric.Services* extends the web socket server for Service Fabric hosting.
4. *WebSocketServer.ServiceFabric.Clients* extends Service Fabric client interfaces for web sockets communications (can be used on its own). 
5. *WebSocketServer.Tests* contains the unit tests that verifies the entire thing (except Service Fabric that is verified in the sample).
6. Additionally:
	1. Sample Service Fabric Service
	2. Sample Service Fabric Client