﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoreNFC;
using UIKit;
using Foundation;
using CoreFoundation;
using Google.Protobuf;
using NFCProject.Services;
using NFCProject.Pages;
using Xamarin;


namespace NFCProject.iOS
{
    public class WriteScan : NFCNdefReaderSessionDelegate, IWriteScan
    {
        string NetID; 
        string NetChan;
        string NodeConfig;
        string OperMode;
        string EncKey;
        string AuthKey;
        string UpdateRate;

        bool NetIDBool;
        bool NetChanBool;
        bool NodeConfigBool;
        bool OperModeBool;
        bool EncKeyBool;
        bool AuthKeyBool;
        bool UpdateRateBool;

        byte[] trimmedResult;

        INFCNdefTag tag;
        NFCNdefReaderSession Session;

        public override void DidDetect(NFCNdefReaderSession session, NFCNdefMessage[] messages)
        {
            //This is left empty on purpose because it will never be called
        }

        [Foundation.Export("readerSession:didDetectTags:")]
        public override void DidDetectTags(NFCNdefReaderSession session, INFCNdefTag[] tags)
        {
            tag = tags[0];
            session.ConnectToTag(tag, delegate { });

            Action<NFCNdefMessage, NSError> readNonce;
            readNonce = EncryptNonce;

            tag.ReadNdef(readNonce);

        }

        public uint calculateChksum() 
        {
            int chksum = 0;

            chksum += Convert.ToInt32(NetID);
            chksum += Convert.ToInt32(NetChan);
            chksum += Convert.ToInt32(NodeConfig);
            chksum += Convert.ToInt32(OperMode);
            for (int i = 0; i < 16; i++) {
                chksum += hexToByte(EncKey)[i]&0xff;
            }
            for (int i = 0; i < 16; i++)
            {
                chksum += hexToByte(AuthKey)[i] & 0xff;
            }

            chksum += NetIDBool ? 1 : 0;
            chksum += NetChanBool ? 1 : 0;
            chksum += NodeConfigBool ? 1 : 0;
            chksum += OperModeBool ? 1 : 0;
            chksum += EncKeyBool ? 1 : 0;
            chksum += AuthKeyBool ? 1 : 0;

            return Convert.ToUInt32(chksum);
        }

        

        private void EncryptNonce(NFCNdefMessage message, NSError error)
        {
            NSData readPayload = message.Records[0].Payload;
            byte[] bytes = readPayload.ToArray();

            RX1_NFC_Reply nfcReply;
            nfcReply = RX1_NFC_Reply.Parser.ParseFrom(bytes);
            byte[] nonce = nfcReply.Nonce.ToByteArray();
            Console.WriteLine("Nonce: " + nfcReply);

            //Generate the key and IV for encryption
            byte[] Key = hexToByte("2b7e151628aed2a6abf7158809cf4f3c");
            byte[] IV = hexToByte("000102030405060708090a0b0c0d0e0f");

            CryptoHandler cryptoHandler = new CryptoHandler();

            byte[] encryptedNonce = cryptoHandler.Encrypt(nonce, Key, IV); //Encrypt the nonce using AES128 CBC encryption (with PKCS7Padding)

            //Trim the encrypted nonce to a length of 16 bytes
            trimmedResult = new byte[16];

            for (int i = 0; i < 16; i++)
            {
                trimmedResult[i] = encryptedNonce[i];
            }

            Console.WriteLine("dds");
                
            NodeConfiguration nodeConfiguration;
            NodeOperatingMode operatingMode;

            #region fix garbage later
            if (NodeConfig == "0")
            {
                nodeConfiguration = NodeConfiguration.Desk1M;
            }
            else if (NodeConfig == "1")
            {
                nodeConfiguration = NodeConfiguration.Desk2M;
            }
            else if (NodeConfig == "2")
            {
                nodeConfiguration = NodeConfiguration.Ceiling1M;
            }
            else
            {
                nodeConfiguration = NodeConfiguration.Ceiling2M;
            }

            if (OperMode == "0")
            {
                operatingMode = NodeOperatingMode.Run;
            }
            else
            {
                operatingMode = NodeOperatingMode.Inventory;
            }
            #endregion fix garbage later

            RX1_NFC_Request nfcRequest = new RX1_NFC_Request
            {
                RequestType = RX1_NFC_Request.Types.NFCRequestType.SetNodeConfig,
                EncryptedNonce = ByteString.CopyFrom(trimmedResult),
                NodeConfig = new RX1_NFC_Config
                {
                    NetworkID = (Convert.ToUInt32(NetID)),
                    HasNetworkID = NetIDBool,
                    NetworkChannel = (Convert.ToUInt32(NetChan)),
                    HasNetworkChannel = NetChanBool,
                    NodeConfiguration = nodeConfiguration,
                    HasNodeConfiguration = NodeConfigBool,
                    OperatingMode = operatingMode,
                    HasOperatingMode = OperModeBool,
                    EncryptionKey = (ByteString.CopyFrom(hexToByte(EncKey))),
                    HasEncryptionKey = EncKeyBool,
                    AuthenticationKey = (ByteString.CopyFrom(hexToByte(AuthKey))),
                    HasAuthenticationKey = AuthKeyBool

                },
                Chksum = calculateChksum()

            };

            string nfcReplyPayload = nfcRequest.ToString(); //Convert to a request to a string so it can be written

            //Create a payload/message and write it.
            NFCNdefPayload writePayload = NFCNdefPayload.CreateWellKnownTypePayload(nfcReplyPayload, NSLocale.CurrentLocale);
            NFCNdefMessage writeMessage = new NFCNdefMessage(new NFCNdefPayload[] { writePayload });
            tag.WriteNdef(writeMessage, delegate { Console.WriteLine("Write succesful"); });

            System.Threading.Thread.Sleep(1000);

            Action<NFCNdefMessage, NSError> readNodeReply;
            readNodeReply = ValidateWrite;
            tag.ReadNdef(ValidateWrite);

        }

        public void ValidateWrite(NFCNdefMessage message, NSError error)
        {
            NSData readPayload = message.Records[0].Payload;
            Console.WriteLine(readPayload);
            byte[] bytes = readPayload.ToArray();

            RX1_NFC_Reply nfcSecondReply;
            nfcSecondReply = RX1_NFC_Reply.Parser.ParseFrom(bytes);

            if (nfcSecondReply.SetNodeConfigAcknowledge)
            {
                Console.WriteLine("Data Written to Node Successfully.  Node will apply settings in 5 seconds and subsequently reset");
            }

            Session.InvalidateSession();
        }

        public override void DidInvalidate(NFCNdefReaderSession session, NSError error)
        {
            //Purposefuly left empty
        }

        public void StartWriteScan(string NetIDTemp, string NetChanTemp, string NodeConfigTemp, string OperModeTemp, string EncKeyTemp, string AuthKeyTemp, string UpdateRateTemp, bool NetIDBoolTemp, bool NetChanBoolTemp, bool NodeConfigBoolTemp, bool OperModeBoolTemp, bool EncKeyBoolTemp, bool AuthKeyBoolTemp, bool UpdateRateBoolTemp)
        {
            NetID = NetIDTemp;
            NetChan = NetChanTemp;
            NodeConfig = NodeConfigTemp;
            OperMode = OperModeTemp;
            EncKey = EncKeyTemp;
            AuthKey = AuthKeyTemp;
            UpdateRate = UpdateRateTemp;
            NetIDBool = NetIDBoolTemp;
            NetChanBool = NetChanBoolTemp;
            NodeConfigBool = NodeConfigBoolTemp;
            OperModeBool = OperModeBoolTemp;
            EncKeyBool = EncKeyBoolTemp;
            AuthKeyBool = AuthKeyBoolTemp;
            UpdateRateBool = UpdateRateBoolTemp;

            Console.WriteLine("StartWrite");
            Session = new NFCNdefReaderSession(this, DispatchQueue.CurrentQueue, false);
            Session.BeginSession();
        }
        public static byte[] hexToByte(String s)
        {
            int length = s.Length;
            byte[] data = new byte[length / 2];
            for (int i = 0; i < length; i += 2)
            {
                data[i / 2] = Convert.ToByte(s.Substring(i, 2), 16);
            }

            return data;
        }
    }
}