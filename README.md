ActuallyWorkingWebSockets
=========================

This library implements [RFC6455] WebSockets in C#. It's based entirely on the [TPL], so it scales pretty well.

Text messages are sent and returned in one piece as `string`s while binary messages are always received as `Stream`s. Just take a look at the `TestServer` to see how you can use it.

[RFC6455]: https://tools.ietf.org/html/rfc6455
[TPL]: http://msdn.microsoft.com/en-us/library/vstudio/dd460717.aspx
