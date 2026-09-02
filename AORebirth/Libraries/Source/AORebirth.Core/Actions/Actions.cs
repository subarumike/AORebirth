#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace AORebirth.Core.Actions
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Core.Entities;
    using AORebirth.Core.Requirements;
    using AORebirth.Enums;

    #endregion

    /// <summary>
    /// AOActions covers all action types, with their reqs
    /// </summary>
    [Serializable]
    public class AOAction : IAOAction
    {
        #region Constructors and Destructors

        /// <summary>
        /// </summary>
        public AOAction()
        {
            this.Requirements = new List<Requirement>(15);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Type of Action (constants in ItemLoader)
        /// </summary>
        public ActionType ActionType { get; set; }

        /// <summary>
        /// List of Requirements for this action
        /// </summary>
        public List<Requirement> Requirements { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="entity">
        /// </param>
        /// <returns>
        /// </returns>
        public bool CheckRequirements(IInstancedEntity entity)
        {
            if (this.Requirements == null || this.Requirements.Count == 0)
            {
                return true;
            }

            // Legacy AOAction evaluation: only ChildOperator.And links are enforced here.
            // Or-linked profession alternatives and single-req templates stay lenient until
            // full requirement runtime matches live AO event evaluation.
            bool result = true;
            foreach (Requirement requirement in this.Requirements)
            {
                if (requirement.ChildOperator == Operator.And)
                {
                    result &= requirement.CheckRequirement(entity);
                }

                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluates requirements and returns a diagnostic summary when they fail.
        /// </summary>
        public bool TryCheckRequirements(IInstancedEntity entity, out string failureDetail)
        {
            failureDetail = null;
            if (this.CheckRequirements(entity))
            {
                return true;
            }

            failureDetail = this.DescribeRequirementResults(entity);
            return false;
        }

        private string DescribeRequirementResults(IInstancedEntity entity)
        {
            List<string> parts = new List<string>(this.Requirements.Count);
            foreach (Requirement requirement in this.Requirements)
            {
                if (Requirement.IsRequirementLinkOperator(requirement)
                    || requirement.ChildOperator != Operator.And)
                {
                    continue;
                }

                bool passed = requirement.CheckRequirement(entity);
                int actual = 0;
                try
                {
                    actual = entity.Stats[requirement.Statnumber].Value;
                }
                catch
                {
                }

                parts.Add(
                    string.Format(
                        "{0}=>{1} actual={2}",
                        requirement,
                        passed ? "pass" : "FAIL",
                        actual));
            }

            if (parts.Count == 0)
            {
                return "requirement chain failed";
            }

            return string.Join("; ", parts.ToArray());
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        internal AOAction Copy()
        {
            AOAction copy = new AOAction();
            copy.ActionType = this.ActionType;
            foreach (Requirement requirements in this.Requirements)
            {
                copy.Requirements.Add(requirements.Copy());
            }

            return copy;
        }

        #endregion
    }
}