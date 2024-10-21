# Jannesen.PushNotifications

Library fo sending push notifications to iOS (using APN) and Android (using firebase).

Because the Apple Push Notification is unreliable by design. This library only implements de minimal to send a push notification to wakeup the APP on de device. It is up to de APP to contect te server to exchange de data.

It usage the legacy APN interface and Firebase HTTPS POST to send notifications.

Not existing devices are reported back.

The library is fully async.
