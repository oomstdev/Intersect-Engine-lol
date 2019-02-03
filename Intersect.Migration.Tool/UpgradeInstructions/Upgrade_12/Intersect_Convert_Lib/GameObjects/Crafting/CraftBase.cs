﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Intersect.Migration.UpgradeInstructions.Upgrade_12.Intersect_Convert_Lib.GameObjects.Crafting
{
    public class CraftBase : DatabaseObject<CraftBase>
    {
        [JsonIgnore]
        [Column("Ingredients")]
        public string IngredientsJson
        {
            get => JsonConvert.SerializeObject(Ingredients, Formatting.None);
            protected set => Ingredients = JsonConvert.DeserializeObject<List<CraftIngredient>>(value);
        }
        [NotMapped]
        public List<CraftIngredient> Ingredients = new List<CraftIngredient>();

        [JsonProperty(Order = -3)]
        public Guid ItemId { get; set; }
        [JsonProperty(Order = -2)]
        public int Time { get; set; }

        [JsonConstructor]
        public CraftBase(Guid id) : base(id)
        {
            Name = "New Craft";
        }

        //Parameterless constructor for EF
        public CraftBase()
        {
            Name = "New Craft";
        }
    }

    public class CraftIngredient
    {
        public Guid ItemId;
        public int Quantity = 1;

        public CraftIngredient(Guid itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public ItemBase GetItem()
        {
            return ItemBase.Get(ItemId);
        }
    }
}