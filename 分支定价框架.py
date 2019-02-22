分支定价框架
Branch-and-Price
{
	initial Solution
	BuildRMP()
	TreeNode root_node = new TreeNode() 
	stack_to_branch.Push(root_node)
												TreeNode
	CG(root_node)								{
													double obj_value_
	Branch-and-Bound(root_node)						List<var> fixed_vars_       //固定变量 var = 1
													List<var> not_fixed_vars_   //待固定的vars,分支时选择其中value最大的来分支。按value降序排列，所以可以用优先队列优化
												}			
}

Branch-and-Bound(TreeNode)
{
	while(not met termination)  //跳出条件 (不一定必须最优，可以是启发式)
	{	
		if(not Feasible(TreeNode))
		{
			if(TreeNode.obj_value_ > UB)      //说明该点不必向下分支了
			{
				Backtrack(TreeNode.fixed_vars_)  //所以回溯到上一个点进行分支             						Backtrack(fixed_vars_) //有问题，得改,删去上一次分支固定的var即可
				Branch(TreeNode)  //实际是对变量进行分支														{                                                            
				continue;																							fixed_vars_.RemoveLatest()
			}																									}	
																												
			  //若(TreeNode.obj_value_ <= UB)  //说明有希望，继续在该点进行分支									
			LB = TreeNode.obj_value_																					Branch(TreeNode)
																														{
			if(TreeNode.not_fixed_vars_.Count == 0)  //若该点没有可分支的var,回溯到上一个点进行分支	y						TreeNode.fixed_vars_.Add(not_fixed_vars_.RemoveMax())
				Backtrack(TreeNode.fixed_vars_)		 //应该不存在该情况，因为没有var可分支，则说明所有var=1,可以结束了		CG(TreeNode)
			Branch(TreeNode)																							}
			continue;						
		}
		
		//若可行
		if(TreeNode.obj_value_ < UB)  //继续更新UB
		{
			UB = TreeNode.obj_value_
			RecordCurrentSolution()
			
			if((UB - LB)/UB < GAP) //最优,结束循环
			{
				terminate
				continue;
			}			
			Backtrack(TreeNode.fixed_vars_)
			Branch(TreeNode)
			continue;
		}
		  //若TreeNode.obj_value_ >= UB，无须再在该点进行分支，回溯
		Backtrack(stack_to_branch)
		Branch(TreeNode)
		
	}
}