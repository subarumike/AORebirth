namespace ZoneEngine.Core.Playfields
{
    using System;
    using System.Linq;

    using AORebirth.Core.Entities;
    using AORebirth.Interfaces;

    using SmokeLounge.AOtomation.Messaging.GameData;

    using ZoneEngine.Core.Functions;
    using ZoneEngine.Core.InternalMessages;

    internal sealed class PlayfieldEnvironmentFunctionRuntimeService
    {
        internal void ExecuteFunction(
            IMExecuteFunction imExecuteFunction,
            Func<Identity, INamedEntity> findNamedEntity,
            Action<Character, string> sendNoValidTargetMessage)
        {
            ITargetingEntity user = (ITargetingEntity)findNamedEntity(imExecuteFunction.User);
            INamedEntity target;

            // TODO: Go over the targets, they can return item templates, inventory entries etc too
            switch (imExecuteFunction.Function.Target)
            {
                case 1:
                    target = (INamedEntity)user;
                    break;
                case 2:
                    throw new NotImplementedException("Target Wearer not implemented yet");
                case 3:
                    target = findNamedEntity(user.SelectedTarget);
                    break;
                case 14:
                    target = findNamedEntity(user.FightingTarget);
                    break;
                case 19: // Perhaps (if issued from a item) its the item itself
                    target = (INamedEntity)user;
                    break;
                case 23:
                    target = findNamedEntity(user.SelectedTarget);
                    break;
                case 26:
                    target = (INamedEntity)user;
                    break;
                case 100:
                    target = (INamedEntity)user;
                    break;
                default:
                    throw new NotImplementedException(
                        "Unknown target encountered: Target#:" + imExecuteFunction.Function.Target);
            }

            if (target == null)
            {
                Character character = user as Character;
                if (character != null)
                {
                    if (character.Controller.Client != null)
                    {
                        sendNoValidTargetMessage(character, "No valid target found");
                    }

                    return;
                }
            }

            FunctionCollection.Instance.CallFunction(
                imExecuteFunction.Function.FunctionType,
                (INamedEntity)user,
                (INamedEntity)user,
                target,
                imExecuteFunction.Function.Arguments.Values.ToArray());
        }
    }
}
