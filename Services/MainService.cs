using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatService.Entities;
using ChatService.Protos;
using Google.Protobuf;
using Grpc.Core;
using IdentityService.Protos;
using NotificationService.Protos;
using UserService.Protos;
using Message = ChatService.Entities.Message;

namespace ChatService.Services
{
    public class ActiveRequest
    {
        public string UserId { get; }
        public IServerStreamWriter<Event> ResponseStream { get; }
    }

    public class MainService : Chat.ChatBase
    {
        // an instance of service is created for every request, keep this static so it is accessible across requests
        private static readonly ConcurrentDictionary<string, IServerStreamWriter<Event>> ActiveRequests = new ConcurrentDictionary<string, IServerStreamWriter<Event>>();
        private readonly Notification.NotificationClient _notificationClient;
        private readonly User.UserClient _userClient;
        private readonly McsvChatDbContext _db;

        // constructor for dependency injection
        public MainService(Notification.NotificationClient notificationClient, User.UserClient userClient,
            McsvChatDbContext db)
        {
            _notificationClient = notificationClient;
            _userClient = userClient;
            _db = db;
        }

        public override async Task EventStream(IAsyncStreamReader<Event> requestStream,
            IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            try
            {
                while (await requestStream.MoveNext())
                {
                    var currentEvent = requestStream.Current;
                    var senderInfo = await _userClient.GetUserInfoAsync(new GetUserInfoRequest
                        {Userid = currentEvent.SenderInfo.Userid});
                    // check if this is an initial message that is sent on the first connection
                    if (currentEvent.SenderInfo.IsInit)
                    {
                        ActiveRequests.TryAdd(currentEvent.SenderInfo.Userid, responseStream);
                    }
                    else if (currentEvent.Message != null)
                    {
                        var message =
                            Message.CreateMessageFromRequest(currentEvent.Message, currentEvent.SenderInfo.Userid, _db);
                        await _db.Messages.AddAsync(message);
                        // get the active request if the user is currently subscribed
                        ActiveRequests.TryGetValue(currentEvent.Message.ReceiverUserId,
                            out var receiverResponse);
                        // user is currently online!
                        if (receiverResponse != null)
                        {
                            // just send the entire event over
                            await receiverResponse.WriteAsync(currentEvent);
                        }
                        else
                        {
                            // change the notification templates depending on group or no group
                            if (currentEvent.Message.GroupId == null)
                            {
                                // send user a notification when they are offline
                                await _notificationClient.SendNotificationByUserIdAsync(new UserIdNotificationRequest
                                {
                                    Message = $"New Message: {currentEvent.Message.Message_}",
                                    Title = $"{senderInfo.UserName}",
                                    Userid = senderInfo.Userid
                                });
                            }
                            else
                            {
                                // send user a notification when they are offline
                                await _notificationClient.SendNotificationByUserIdAsync(new UserIdNotificationRequest
                                {
                                    Message = $"New Message: {currentEvent.Message.Message_}",
                                    Title = $"{senderInfo.UserName}",
                                    Userid = senderInfo.Userid
                                });
                            }
                        }

                        await _db.SaveChangesAsync();
                    }
                    else if (currentEvent.GroupCreated != null)
                    {
                    }
                }
                // unfortunately, by this stage we might not have the key (userid) so we have to take the inefficient route and find it using linq
                // then remove it using the key, which searches for it again. Mitigate this by running it in an asynchronous task
                // so we can let this run while we remove the active user.
                await Task.Run(() =>
                {
                    // remove the request as an active request before it ends
                    var activeRequest = ActiveRequests.FirstOrDefault(x => x.Value == responseStream);
                    ActiveRequests.Remove(activeRequest.Key, out var request);
                });

            }
            catch (Exception e)
            {
                // same thing here
                await Task.Run(() =>
                {
                    // remove any references to the current request on error
                    var activeRequest = ActiveRequests.FirstOrDefault(x => x.Value == responseStream);
                    ActiveRequests.Remove(activeRequest.Key, out var request);
                });
                
            }
        }

        public override async Task<NewMessagesResponse> GetUnreadMessages(NewMessagesRequest request,
            ServerCallContext context)
        {
            // run all these in a separate thread so we can continue with other tasks while this is running
            return await Task.Run(() =>
            {
                return new NewMessagesResponse
                {
                    Message =
                    {
                        _db.Messages.Where(x =>
                                x.UsersReads.Any(o =>
                                    o.UserId == request.Userid && o.MessageStatus != 2))
                            .Select(map => new Protos.Message
                            {
                                Id = map.Uuid.ToString(),
                                // only create a media if the message's mediaref is not null, otherwise, just set it to null
                                Media = map.MediaRef != null
                                    ? new Protos.Media
                                    {
                                        MediaUrl = map.MediaRefNavigation.Url,
                                        MimeType = map.MediaRefNavigation.MimeType,
                                        SizeBytes = (ulong) map.MediaRefNavigation.Size
                                    }
                                    : null,
                                Message_ = map.Text,
                                // only get the navigation if the group ref exists
                                GroupId = map.GroupRef != null ? map.GroupRefNavigation.Uuid.ToString() : null,
                                IsForward = map.IsForward,
                                MessageStatus = (uint) map.UsersReads.First().MessageStatus,
                                ReplyId = map.ReplyMessageRefNavigation.Uuid.ToString(),
                                // receiver id is nullable, no need ternary
                                ReceiverUserId = map.RecieverId,
                            })
                    }
                };
            });
        }

        public override async Task<AllMessagesResponse> GetAllMessages(AllMessagesRequest request,
            ServerCallContext context)
        {
            // run all these in a separate thread so we can continue with other tasks while this is running
            return await Task.Run(() =>
            {
                // same as get unread, just without the unread only filter
                return new AllMessagesResponse
                {
                    Message =
                    {
                        _db.Messages.Where(x =>
                                x.UsersReads.Any(o =>
                                    o.UserId == request.Userid))
                            .Select(map => new Protos.Message
                            {
                                Id = map.Uuid.ToString(),
                                // only create a media if the message's mediaref is not null, otherwise, just set it to null
                                Media = map.MediaRef != null
                                    ? new Protos.Media
                                    {
                                        MediaUrl = map.MediaRefNavigation.Url,
                                        MimeType = map.MediaRefNavigation.MimeType,
                                        SizeBytes = (ulong) map.MediaRefNavigation.Size
                                    }
                                    : null,
                                Message_ = map.Text,
                                // only get the navigation if the group ref exists
                                GroupId = map.GroupRef != null ? map.GroupRefNavigation.Uuid.ToString() : null,
                                IsForward = map.IsForward,
                                MessageStatus = (uint) map.UsersReads.First().MessageStatus,
                                ReplyId = map.ReplyMessageRefNavigation.Uuid.ToString(),
                                // receiver id is nullable, no need ternary
                                ReceiverUserId = map.RecieverId,
                            })
                    }
                };
            });
        }
    }
}