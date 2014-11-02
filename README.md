ActuallyWorkingWebSockets
=========================

This library implements [RFC6455] WebSockets in C#. It's based entirely on the [TPL] (`async`+`await`), so it scales pretty well.

Text messages are sent and returned in one piece as `string`s while binary messages are always received as `Stream`s. Just take a look at the `TestServer` to see how you can use it.

The current API design prefers simplicity and it has one significant drawback because of that: You have to specify in advance what kind of message you would like to receive. But if you really need it, you can easily work around that. While all the `async` stuff can sometimes be a bit confusing, the code isn't that complicated.

[RFC6455]: https://tools.ietf.org/html/rfc6455
[TPL]: http://msdn.microsoft.com/en-us/library/vstudio/dd460717.aspx
