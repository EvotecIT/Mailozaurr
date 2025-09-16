using System;

using System.Collections.Generic;

using System.Threading;

using System.Threading.Tasks;



namespace Mailozaurr.Tests;



public sealed class PendingMessageSenderFactoryTests {

    private sealed class TestSender : IPendingMessageSender {

        public Task SendAsync(PendingMessageRecord record, CancellationToken ct) => Task.CompletedTask;

    }



    [Fact]

    public void GetSender_ReturnsRegisteredSender() {

        var sender = new TestSender();

        var factory = new PendingMessageSenderFactory(new Dictionary<EmailProvider, IPendingMessageSender> {

            { EmailProvider.SendGrid, sender }

        });



        var record = new PendingMessageRecord { Provider = EmailProvider.SendGrid };



        var result = factory.GetSender(record);



        Assert.Same(sender, result);

    }



    [Fact]

    public void GetSender_ReturnsNoopWhenProviderMissing() {

        var factory = new PendingMessageSenderFactory();

        var record = new PendingMessageRecord { Provider = EmailProvider.Mailgun };



        var result = factory.GetSender(record);



        Assert.IsType<NoopPendingMessageSender>(result);

    }



    [Fact]

    public void GetSender_ThrowsWhenRecordIsNull() {

        var factory = new PendingMessageSenderFactory();



        Assert.Throws<ArgumentNullException>(() => factory.GetSender(null!));

    }

}

