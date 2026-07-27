namespace CanonFlow.Assurance.Verification

open System
open System.Text
open CanonFlow.Assurance

module ReceiptVerifier =
    
    let verifyOffline (canonicalPayloadJson: string) (pubKeyBytes: byte[]) (signatureBase64: string) =
        let sigBytes = Convert.FromBase64String(signatureBase64)
        let payloadBytes = Encoding.UTF8.GetBytes(canonicalPayloadJson)
        
        Ed25519Verify.verify (PublicKey pubKeyBytes) payloadBytes (Signature sigBytes)

