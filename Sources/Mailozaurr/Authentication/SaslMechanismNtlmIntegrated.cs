using NSspi;
using NSspi.Contexts;
using NSspi.Credentials;

namespace Mailozaurr {
    /// <summary>
    /// The NTLM Integrated Auth SASL mechanism.
    /// </summary>
    /// <remarks>
    /// A SASL mechanism based on NTLM using the credentials of the current user 
    /// via Windows Integrated Authentication (SSPI).
    /// </remarks>
    /// <inheritdoc cref="SaslMechanism"/>
    public class SaslMechanismNtlmIntegrated : SaslMechanism {
        enum LoginState {
            Initial,
            Challenge
        }

        LoginState state;
        ClientContext sspiContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="SaslMechanismNtlmIntegrated"/> class.
        /// </summary>
        /// <remarks>
        /// Creates a new NTLM Integrated Auth SASL context.
        /// </remarks>
        public SaslMechanismNtlmIntegrated() : base(string.Empty, string.Empty) {
        }

        /// <inheritdoc cref="SaslMechanism.MechanismName"/>
        public override string MechanismName => "NTLM";

        /// <inheritdoc cref="SaslMechanism.SupportsInitialResponse"/>
        public override bool SupportsInitialResponse => true;

        /// <summary>
        /// The authenticated user name.
        /// </summary>
        public virtual string AuthenticatedUserName => sspiContext.ContextUserName;

        /// <inheritdoc cref="SaslMechanism.Challenge(byte[], int, int, CancellationToken)"/>
        /// <exception cref="InvalidOperationException">
        /// The SASL mechanism is already authenticated.
        /// </exception>
        protected override byte[] Challenge(byte[] token, int startIndex, int length, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsAuthenticated) {
                throw new InvalidOperationException();
            }

            InitializeSSPIContext();

            byte[] serverResponse;

            if (state == LoginState.Initial) {
                sspiContext.Init(null, out serverResponse);
                state = LoginState.Challenge;
            } else {
                sspiContext.Init(token, out serverResponse);
                IsAuthenticated = true;
            }

            return serverResponse;
        }

        private void InitializeSSPIContext() {
            if (sspiContext != null) {
                return;
            }

            var credential = new ClientCurrentCredential(PackageNames.Ntlm);

            sspiContext = new ClientContext(
                credential,
                string.Empty,
                ContextAttrib.InitIntegrity
                | ContextAttrib.ReplayDetect
                | ContextAttrib.SequenceDetect
                | ContextAttrib.Confidentiality);
        }


        public override void Reset() {
            state = LoginState.Initial;
            base.Reset();
        }
    }
}