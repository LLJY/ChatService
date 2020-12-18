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
using Microsoft.EntityFrameworkCore;
using NotificationService.Protos;
using UserService.Protos;
using Message = ChatService.Entities.Message;

namespace ChatService.Services
{
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
                    Console.WriteLine(currentEvent.ToString());
                    var userInfoRequest = new GetUserInfoRequest
                    {
                        Userid = currentEvent.SenderInfo.Userid
                    };
                    var senderInfo = _userClient.GetUserInfo(userInfoRequest);
                    // check if this is an initial message that is sent on the first connection'
                    //currentEvent.SenderInfo
                    if (currentEvent.SenderInfo.IsInit)
                    {
                        ActiveRequests.TryAdd(currentEvent.SenderInfo.Userid, responseStream);
                    }
                    else if (currentEvent.Message != null)
                    {
                        Console.WriteLine(currentEvent.Message.Id);
                        var message =
                            Message.CreateMessageFromRequest(currentEvent.Message, currentEvent.SenderInfo.Userid, _db);
                        // if the group is not null, add many fields for user read
                        if (message.GroupRef != null)
                        {
                            foreach (var member in message.GroupRefNavigation.GroupMembers)
                            {
                                await _db.UsersReads.AddAsync(new UsersRead
                                {
                                    MessageRefNavigation = message,
                                    MessageStatus = 0,
                                    UserId = member.Userid
                                });
                                // change the notification templates depending on group or no group
                                // send user a notification when they are offline, otherwise send them the event
                                ActiveRequests.TryGetValue(currentEvent.Message.ReceiverUserId,
                                    out var receiver);
                                if(receiver != null){
                                    try
                                    {
                                        await _notificationClient.SendNotificationByUserIdAsync(
                                            new UserIdNotificationRequest
                                            {
                                                Message = $"New Message: {currentEvent.Message.Message_}",
                                                Title = $"{senderInfo.UserName}",
                                                Userid = member.Userid
                                            });
                                    }
                                    catch (Exception)
                                    {
                                        // don't do anything, sometimes unregistered tokens don't get notified.
                                    }
                                }
                            }
                        }
                        // if group is null, this is a peer to peer message, only add one read.
                        else
                        {
                            await _db.UsersReads.AddAsync(new UsersRead
                            {
                                MessageRefNavigation = message,
                                MessageStatus = 0,
                                UserId = requestStream.Current.Message.ReceiverUserId
                            });
                        }
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
                            // send user a notification when they are offline
                                await _notificationClient.SendNotificationByUserIdAsync(new UserIdNotificationRequest
                                {
                                    Message = $"New Message: {currentEvent.Message.Message_}",
                                    Title = $"{senderInfo.UserName}",
                                    Userid = currentEvent.Message.ReceiverUserId
                                });
                            
                        }

                        await _db.SaveChangesAsync();
                    }
                    else if (currentEvent.GroupCreated != null)
                    {
                        //var groupMembers = currentEvent.GroupCreated.
                        var group = new Group
                        {
                            // empty message list
                            Messages = { },
                            Title = currentEvent.GroupCreated.Title,
                            Uuid = Guid.NewGuid(),
                        };
                        await _db.Groups.AddAsync(group);
                        var groupMembers = currentEvent.GroupCreated.GroupMemberIds.Select(x => new GroupMember
                        {
                            Userid = x,
                            GroupRefNavigation = group,
                            // everyone is not admin by default
                            IsAdmin = false,
                        });
                        await _db.GroupMembers.AddRangeAsync(groupMembers);
                        var creatorMember = new GroupMember
                        {
                            Userid = currentEvent.GroupCreated.GroupCreator,
                            IsAdmin = true,
                            GroupRefNavigation = group
                        };
                        foreach (var memberIds in currentEvent.GroupCreated.GroupMemberIds)
                        {
                            // get the active request if the user is currently subscribed
                            ActiveRequests.TryGetValue(memberIds,
                                out var receiverResponse);
                            if (receiverResponse != null)
                            {
                                // just send the entire event over
                                await receiverResponse.WriteAsync(currentEvent);
                            }
                            else
                            {
                                // send user a notification when they are offline
                                await _notificationClient.SendNotificationByUserIdAsync(new UserIdNotificationRequest
                                {
                                    Message = $"{currentEvent.GroupCreated.Title}",
                                    Title = $"You have been added to a group!",
                                    Userid = memberIds
                                });
                            }
                        }
                        await _db.SaveChangesAsync();
                    }else if (currentEvent.MessageRead != null)
                    {
                        Console.WriteLine("message read");
                        _db.UsersReads.First(x=>x.MessageRefNavigation.Uuid == Guid.Parse(currentEvent.MessageRead.MessageId)).MessageStatus = (int)currentEvent.MessageRead.MessageStatus;
                        // get the active request if the user is currently subscribed
                        var message = _db.Messages.First(x =>
                            x.Uuid == Guid.Parse(currentEvent.MessageRead.MessageId));
                        ActiveRequests.TryGetValue(message.AuthorId,
                            out var receiverResponse);
                        // user is currently online!
                        if (receiverResponse != null)
                        {
                            // just send the entire event over
                            await receiverResponse.WriteAsync(currentEvent);
                        }
                        // no need to send notification for read receipts
                        
                        
                        await _db.SaveChangesAsync();
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
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
                                GroupId = map.GroupRef != null ? map.GroupRefNavigation.Uuid.ToString() : "",
                                IsForward = map.IsForward,
                                MessageStatus = (uint) map.UsersReads.First().MessageStatus,
                                ReplyId = map.ReplyMessageRefNavigation.Uuid.ToString()?? "",
                                // receiver id is nullable, no need ternary
                                ReceiverUserId = map.ReceiverId,
                                SenderInfo = new SenderInfo
                                {
                                    Userid = map.AuthorId,
                                    // isinit can be ignored
                                    IsInit = false
                                },
                                DatePostedUnixTimestamp = ((DateTimeOffset)map.Dateposted).ToUnixTimeMilliseconds()
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
                                x.AuthorId == request.Userid || x.ReceiverId == request.Userid || x.GroupRefNavigation.GroupMembers.Any(x=>x.Userid == request.Userid))
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
                                GroupId = map.GroupRef != null ? map.GroupRefNavigation.Uuid.ToString() : "",
                                IsForward = map.IsForward,
                                MessageStatus = (uint) map.UsersReads.First().MessageStatus,
                                ReplyId = map.ReplyMessageRefNavigation.Uuid.ToString()?? "",
                                // receiver id is nullable, no need ternary
                                ReceiverUserId = map.ReceiverId,
                                SenderInfo = new SenderInfo
                                {
                                    Userid = map.AuthorId,
                                    // isinit can be ignored
                                    IsInit = false
                                },
                                DatePostedUnixTimestamp = ((DateTimeOffset)map.Dateposted).ToUnixTimeMilliseconds()
                            })
                    }
                };
            });
        }

        public override async Task<GroupInfo> GetGroupInfo(GetGroupsRequest request, ServerCallContext context)
        {
            try
            {
                var group = await _db.Groups.FirstOrDefaultAsync(x => x.Uuid == Guid.Parse(request.GroupId));
                return new GroupInfo
                {
                    Title = group.Title,
                    GroupId = request.GroupId,
                    GroupImage = group.PhotoRefNavigation != null
                        ? new Protos.Media
                        {
                            MediaUrl = group.PhotoRefNavigation.Url,
                            MimeType = group.PhotoRefNavigation.MimeType,
                            SizeBytes = (ulong) @group.PhotoRefNavigation.Size
                        }
                        : null,
                    GroupMemberIds = {group.GroupMembers.Select(x => x.Userid)}
                };
            }
            catch (NullReferenceException)
            {
                // empty if the group does not exist
                return new GroupInfo
                {

                };
            }
        }
    }
}